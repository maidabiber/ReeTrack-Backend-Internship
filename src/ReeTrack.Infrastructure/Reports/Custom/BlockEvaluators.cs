using System.Globalization;
using ReeTrack.Application.Common.Models.CustomReports;
using ReeTrack.Application.Reports;
using ReeTrack.Infrastructure.Reports.Writers;

namespace ReeTrack.Infrastructure.Reports.Custom;

internal static class BlockEvaluators
{
    public const int MaxBreakdownRows = 500;

    /// <summary>Marks a rolled-up row whose members do not share one currency.</summary>
    public const string MixedCurrencyCode = "MIXED";

    public static ReportBlockResult Evaluate(ReportBlockSpec block, CustomReportContext context) =>
        block switch
        {
            KpiBlockSpec kpi => EvaluateKpi(kpi, context),
            BreakdownBlockSpec breakdown => EvaluateBreakdown(breakdown, context),
            ChartBlockSpec chart => EvaluateChart(chart, context),
            EntriesBlockSpec entries => EvaluateEntries(entries, context),
            NoteBlockSpec note => EvaluateNote(note),
            NarrativeBlockSpec narrative => EvaluateNarrative(narrative),
            _ => throw Application.Common.Exceptions.AppErrors.Validation(
                $"Unsupported block type '{block.GetType().Name}'.")
        };

    private static KpiGroupResult EvaluateKpi(KpiBlockSpec block, CustomReportContext context)
    {
        var mixedMoney = false;
        // Resolved once for the block — the period's currency does not vary per metric.
        var periodCurrency = ResolveSingleCurrency(context.Rows);
        // Every row shares that currency when it is non-null, so no per-metric filtering is needed.
        var input = new MetricInput(context.Rows, context, context.GrandTotalSeconds);

        var cells = block.Metrics.Select(metricId =>
        {
            var metric = MetricCatalog.GetRequired(metricId);
            var isMoney = metric.Unit is MetricUnit.Money or MetricUnit.Rate;
            string? currency = null;

            if (isMoney)
            {
                if (periodCurrency is null && context.Rows.Count > 0)
                {
                    mixedMoney = true;
                    return new KpiCell
                    {
                        Key = metric.Id,
                        Label = metric.Label,
                        Value = null,
                        Unit = metric.Unit,
                        CurrencyCode = null,
                        Display = "—"
                    };
                }

                currency = periodCurrency;
            }

            var value = metric.Aggregate(input);
            return new KpiCell
            {
                Key = metric.Id,
                Label = metric.Label,
                Value = value,
                Unit = metric.Unit,
                CurrencyCode = currency,
                Display = CustomReportDisplay.Format(value, metric.Unit, currency)
            };
        }).ToList();

        return new KpiGroupResult
        {
            Id = block.Id,
            Title = block.Title ?? "KPIs",
            Cells = cells,
            Footnote = mixedMoney
                ? "Money KPIs omitted because the period mixes currencies."
                : null
        };
    }

    private static TableResult EvaluateBreakdown(BreakdownBlockSpec block, CustomReportContext context)
    {
        var dimensions = block.Dimensions.Select(DimensionCatalog.GetRequired).ToList();
        var metrics = block.Metrics.Select(MetricCatalog.GetRequired).ToList();
        var hasMoney = metrics.Any(m => m.Unit is MetricUnit.Money or MetricUnit.Rate);
        // Money metrics must never cross-sum — split every dimension group by currency.
        var groups = GroupRows(context.Rows, dimensions, splitByCurrency: hasMoney);

        var columns = new List<TableColumn>();
        foreach (var dimension in dimensions)
            columns.Add(new TableColumn { Key = dimension.Id, Label = dimension.Label, ColumnType = TableColumnType.Text });
        if (hasMoney)
            columns.Add(new TableColumn { Key = "currency", Label = "Currency", ColumnType = TableColumnType.Text });
        foreach (var metric in metrics)
            columns.Add(new TableColumn
            {
                Key = metric.Id,
                Label = metric.Label,
                ColumnType = CustomReportDisplay.ToColumnType(metric.Unit)
            });
        var metricsById = metrics.ToDictionary(m => m.Id, StringComparer.OrdinalIgnoreCase);
        foreach (var computed in block.Computed)
            columns.Add(new TableColumn
            {
                Key = computed.Id,
                Label = computed.Label,
                ColumnType = ComputedColumnType(computed, metricsById)
            });

        var rawRows = new List<(string Key, string[] Labels, string Currency, Dictionary<string, decimal?> Values, long SortSeconds)>();
        foreach (var group in groups)
        {
            var currency = hasMoney
                ? group.CurrencyCode ?? SummaryReportAnalytics.NoCurrencyCode
                : "";
            var input = new MetricInput(group.Rows, context, context.GrandTotalSeconds);
            var values = new Dictionary<string, decimal?>(StringComparer.OrdinalIgnoreCase);
            foreach (var metric in metrics)
                values[metric.Id] = metric.Aggregate(input);
            foreach (var computed in block.Computed)
                values[computed.Id] = EvaluateComputed(computed, values);

            rawRows.Add((group.Key, group.Labels, currency, values, SumSeconds(group.Rows)));
        }

        // Materialise once — OrderBy is deferred and every re-enumeration re-sorts.
        var ordered = OrderBreakdown(rawRows, block).ToList();

        // The row ceiling applies to an explicit topN too, otherwise `topN: 100000` renders
        // an unbounded table.
        var topN = block.TopN is { } requested ? Math.Min(requested, MaxBreakdownRows) : (int?)null;
        var forcedCap = ordered.Count > MaxBreakdownRows && (topN is null || topN == MaxBreakdownRows);
        if (forcedCap)
            topN = MaxBreakdownRows;

        List<(string Key, string[] Labels, string Currency, Dictionary<string, decimal?> Values, long SortSeconds)> finalRows;
        (string Key, string[] Labels, string Currency, Dictionary<string, decimal?> Values, long SortSeconds)? others = null;

        if (topN is { } n && ordered.Count > n)
        {
            finalRows = ordered.Take(n).ToList();
            // Cap always rolls the rest into Others so totals stay honest.
            if (block.IncludeOthers || forcedCap)
            {
                var rest = ordered.Skip(n).ToList();
                var othersValues = new Dictionary<string, decimal?>(StringComparer.OrdinalIgnoreCase);
                foreach (var metric in metrics)
                {
                    if (metric.Unit is MetricUnit.Percent or MetricUnit.Rate)
                    {
                        othersValues[metric.Id] = null;
                        continue;
                    }

                    othersValues[metric.Id] = rest.Sum(r => r.Values.GetValueOrDefault(metric.Id) ?? 0m);
                }

                foreach (var computed in block.Computed)
                    othersValues[computed.Id] = EvaluateComputed(computed, othersValues);

                var othersLabels = dimensions.Select((_, i) => i == 0 ? "Others" : "").ToArray();
                others = ("others", othersLabels, ResolveMixedCurrency(rest.Select(r => r.Currency)), othersValues, rest.Sum(r => r.SortSeconds));
            }
        }
        else
        {
            finalRows = ordered;
        }

        var tableRows = new List<TableRow>();
        foreach (var row in finalRows)
            tableRows.Add(ToTableRow(row, dimensions, metrics, block.Computed, metricsById, hasMoney));
        if (others is { } o)
            tableRows.Add(ToTableRow(o, dimensions, metrics, block.Computed, metricsById, hasMoney));

        TableRow? totals = null;
        // Others is part of the total, so its currencies count towards the mixing check —
        // otherwise a single-currency top N hides a multi-currency tail behind an honest-looking total.
        var currencies = finalRows
            .Select(r => r.Currency)
            .Concat(others is { } tail ? [tail.Currency] : Array.Empty<string>())
            .Where(c => c.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var mixedCurrency = hasMoney && (currencies.Count > 1 || currencies.Contains(MixedCurrencyCode));
        if (block.ShowTotals && !mixedCurrency)
        {
            var totalValues = new Dictionary<string, decimal?>(StringComparer.OrdinalIgnoreCase);
            var allForTotals = others is { } o2 ? finalRows.Append(o2) : finalRows;
            foreach (var metric in metrics)
            {
                if (metric.Unit is MetricUnit.Percent or MetricUnit.Rate)
                {
                    var input = new MetricInput(context.Rows, context, context.GrandTotalSeconds);
                    totalValues[metric.Id] = metric.Aggregate(input);
                    continue;
                }

                totalValues[metric.Id] = allForTotals.Sum(r => r.Values.GetValueOrDefault(metric.Id) ?? 0m);
            }

            foreach (var computed in block.Computed)
                totalValues[computed.Id] = EvaluateComputed(computed, totalValues);

            var totalLabels = dimensions.Select((_, i) => i == 0 ? "Total" : "").ToArray();
            totals = ToTableRow(
                ("total", totalLabels, currencies.SingleOrDefault() ?? "", totalValues, context.GrandTotalSeconds),
                dimensions,
                metrics,
                block.Computed,
                metricsById,
                hasMoney);
        }

        var footnotes = new List<string>();
        footnotes.AddRange(FanOutNotes(dimensions));
        if (mixedCurrency)
            footnotes.Add("Totals omitted because rows mix currencies.");
        if (forcedCap)
            footnotes.Add($"Showing the top {MaxBreakdownRows} rows; the rest are rolled into Others.");

        return new TableResult
        {
            Id = block.Id,
            Title = block.Title ?? string.Join(" × ", dimensions.Select(d => d.Label)),
            Columns = columns,
            Rows = tableRows,
            Totals = totals,
            Footnote = footnotes.Count == 0 ? null : string.Join(" ", footnotes)
        };
    }

    private static SeriesResult EvaluateChart(ChartBlockSpec block, CustomReportContext context)
    {
        var dimension = DimensionCatalog.GetRequired(block.Dimension);
        var metrics = block.Metrics.Select(MetricCatalog.GetRequired).ToList();
        var hasMoney = metrics.Any(m => m.Unit is MetricUnit.Money or MetricUnit.Rate);
        var groups = GroupRows(context.Rows, [dimension], splitByCurrency: hasMoney)
            .OrderBy(g => g.SortHint)
            .ThenBy(g => g.Labels[0], StringComparer.OrdinalIgnoreCase)
            .ThenBy(g => g.CurrencyCode, StringComparer.OrdinalIgnoreCase)
            .ToList();

        string? topNFootnote = null;
        if (block.TopN is { } n && groups.Count > n)
        {
            // "Top N" means the N largest by the leading metric, not the first N in display
            // order — taking the head of a chronological axis silently drops the recent end.
            var leading = metrics[0];
            var keep = groups
                .OrderByDescending(g => leading.Aggregate(
                    new MetricInput(g.Rows, context, context.GrandTotalSeconds)) ?? 0m)
                .Take(n)
                .Select(g => g.Key)
                .ToHashSet(StringComparer.Ordinal);

            var dropped = groups.Count - n;
            groups = groups.Where(g => keep.Contains(g.Key)).ToList();
            topNFootnote = $"Showing the top {n} by {leading.Label}; {dropped} smaller group(s) hidden.";
        }

        var categories = groups.Select(g =>
            hasMoney && g.CurrencyCode is { } code && code != SummaryReportAnalytics.NoCurrencyCode
                ? $"{g.Labels[0]} ({code})"
                : g.Labels[0]).ToList();
        var series = metrics.Select(metric =>
        {
            var values = groups.Select(group =>
            {
                var input = new MetricInput(group.Rows, context, context.GrandTotalSeconds);
                return metric.Aggregate(input) ?? 0m;
            }).ToList();

            return new NamedSeries
            {
                Key = metric.Id,
                Label = metric.Label,
                Values = values
            };
        }).ToList();

        return new SeriesResult
        {
            Id = block.Id,
            Title = block.Title ?? $"{dimension.Label} trend",
            Kind = block.Kind,
            Categories = categories,
            Series = series,
            Footnote = JoinFootnotes([.. FanOutNotes([dimension]), topNFootnote])
        };
    }

    /// <summary>Entry columns a group subtotal can honestly sum. Everything else stays blank on a subtotal row.</summary>
    private static readonly HashSet<string> SubtotalableColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "hours", "labourCost"
    };

    private static TableResult EvaluateEntries(EntriesBlockSpec block, CustomReportContext context)
    {
        var columns = block.Columns.Select(id => new TableColumn
        {
            Key = id,
            Label = EntryColumnCatalog.All[id],
            ColumnType = id switch
            {
                "date" => TableColumnType.Date,
                "hours" => TableColumnType.Hours,
                "labourCost" => TableColumnType.Money,
                "billable" => TableColumnType.Text,
                _ => TableColumnType.Text
            }
        }).ToList();

        var sorted = SortEntries(context.Rows, block.GroupBy).ToList();
        var ordered = sorted.Take(block.Limit).ToList();

        List<TableRow> rows;
        var truncatedMidGroup = false;
        if (block.GroupBy.Count == 0)
        {
            rows = ordered.Select(row => BuildDetailRow(row, block.Columns, depth: 0)).ToList();
        }
        else
        {
            rows = BuildGroupedRows(ordered, block);

            // The outermost visible group is "partial" — has more rows beyond the limit — when
            // the very next row (dropped by Take) still shares its top-level key. A mismatch
            // there means the cut landed cleanly on a group boundary instead.
            if (sorted.Count > ordered.Count && ordered.Count > 0)
            {
                var nextRow = sorted[ordered.Count];
                var topLevel = block.GroupBy[0];
                truncatedMidGroup = string.Equals(
                    EntryGroupKey(ordered[^1], topLevel),
                    EntryGroupKey(nextRow, topLevel),
                    StringComparison.OrdinalIgnoreCase);
            }
        }

        var footnote = truncatedMidGroup
            ? "The row limit was reached inside a group; its subtotal reflects only the entries shown."
            : null;

        return new TableResult
        {
            Id = block.Id,
            Title = block.Title ?? "Entries",
            Columns = columns,
            Rows = rows,
            Footnote = footnote
        };
    }

    private static TableRow BuildDetailRow(EntryRow row, IReadOnlyList<string> columnIds, int depth)
    {
        var cells = new Dictionary<string, TableCell>(StringComparer.OrdinalIgnoreCase);
        foreach (var column in columnIds)
            cells[column] = BuildEntryCell(row, column);

        return new TableRow { Key = row.EntryId.ToString(), Cells = cells, Kind = TableRowKind.Detail, Depth = depth };
    }

    private static TableCell BuildEntryCell(EntryRow row, string column) => column switch
    {
        "date" => new TableCell { Number = row.Date.DayNumber, Display = ReportFormat.FriendlyDate(row.Date) },
        "user" => TextCell(row.UserName),
        "client" => TextCell(row.ClientLabel),
        "project" => TextCell(row.ProjectLabel),
        "task" => TextCell(row.TaskLabel),
        "tags" => TextCell(string.Join(", ", row.Tags.Select(t => t.Label))),
        "billable" => TextCell(row.IsBillable ? "Yes" : "No"),
        "hours" => new TableCell
        {
            Number = SummaryReportAnalytics.Hours(row.DurationSeconds),
            Display = ReportFormat.HoursLabel(row.DurationSeconds)
        },
        "labourCost" => new TableCell
        {
            Number = row.Cost is null ? null : ReportRounding.Cost(row.Cost.CalculatedCost),
            Display = row.Cost is null
                ? "—"
                : ReportFormat.Money(ReportRounding.Cost(row.Cost.CalculatedCost), row.CurrencyCode)
        },
        "currency" => TextCell(row.CurrencyCode),
        "description" => TextCell(row.Description ?? ""),
        _ => TextCell("")
    };

    /// <summary>Tracks one still-open group while walking the sorted, already-limited rows.</summary>
    private sealed class OpenGroup
    {
        public required string Label;
        public required int Depth;
        public readonly Dictionary<string, decimal> Sums = new(StringComparer.OrdinalIgnoreCase);
        public readonly HashSet<string> Currencies = new(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Walks rows already sorted by <paramref name="block"/>'s groupBy dimensions, emitting a
    /// GroupHeader when a group key changes and a GroupSubtotal when it closes. Only "hours" and
    /// "labourCost" are summed — every other column stays blank on a subtotal, and a subtotal that
    /// would mix currencies is blanked too rather than adding money across currencies.
    /// </summary>
    private static List<TableRow> BuildGroupedRows(IReadOnlyList<EntryRow> ordered, EntriesBlockSpec block)
    {
        var result = new List<TableRow>();
        var open = new List<OpenGroup>();
        var openKeys = new List<string>();
        var levels = block.GroupBy.Count;
        var subtotalCols = block.Columns.Where(SubtotalableColumns.Contains).ToList();

        void CloseFrom(int fromDepthInclusive)
        {
            for (var d = open.Count - 1; d >= fromDepthInclusive; d--)
            {
                result.Add(BuildSubtotalRow(open[d], block.Columns, subtotalCols));
                open.RemoveAt(d);
                openKeys.RemoveAt(d);
            }
        }

        foreach (var row in ordered)
        {
            var keys = block.GroupBy.Select(dimension => EntryGroupKey(row, dimension)).ToList();

            var firstDiff = 0;
            while (firstDiff < open.Count && firstDiff < keys.Count
                   && string.Equals(openKeys[firstDiff], keys[firstDiff], StringComparison.OrdinalIgnoreCase))
                firstDiff++;

            CloseFrom(firstDiff);

            for (var depth = firstDiff; depth < levels; depth++)
            {
                var group = new OpenGroup { Label = keys[depth], Depth = depth };
                open.Add(group);
                openKeys.Add(keys[depth]);
                result.Add(BuildGroupHeaderRow(group, block.Columns));
            }

            foreach (var group in open)
            {
                foreach (var column in subtotalCols)
                {
                    var value = EntryNumericValue(row, column);
                    if (value is { } v)
                        group.Sums[column] = group.Sums.GetValueOrDefault(column) + v;
                }
                if (row.CurrencyCode.Length > 0)
                    group.Currencies.Add(row.CurrencyCode);
            }

            result.Add(BuildDetailRow(row, block.Columns, depth: levels));
        }

        CloseFrom(0);
        return result;
    }

    private static TableRow BuildGroupHeaderRow(OpenGroup group, IReadOnlyList<string> columnIds)
    {
        var cells = new Dictionary<string, TableCell>(StringComparer.OrdinalIgnoreCase);
        var firstColumn = columnIds.FirstOrDefault();
        foreach (var column in columnIds)
            cells[column] = column == firstColumn ? TextCell(group.Label) : TextCell("");

        return new TableRow
        {
            Key = $"group:{group.Depth}:{group.Label}:{Guid.NewGuid()}",
            Cells = cells,
            Kind = TableRowKind.GroupHeader,
            Depth = group.Depth
        };
    }

    private static TableRow BuildSubtotalRow(OpenGroup group, IReadOnlyList<string> columnIds, IReadOnlyList<string> subtotalCols)
    {
        var cells = new Dictionary<string, TableCell>(StringComparer.OrdinalIgnoreCase);
        var firstColumn = columnIds.FirstOrDefault();
        var mixedCurrency = group.Currencies.Count > 1;

        foreach (var column in columnIds)
        {
            if (column == firstColumn)
            {
                cells[column] = TextCell($"Subtotal — {group.Label}");
                continue;
            }

            if (!subtotalCols.Contains(column, StringComparer.OrdinalIgnoreCase))
            {
                cells[column] = TextCell("");
                continue;
            }

            if (string.Equals(column, "labourCost", StringComparison.OrdinalIgnoreCase) && mixedCurrency)
            {
                cells[column] = TextCell("—");
                continue;
            }

            // A column absent from Sums means every row in the group had a null value for it
            // (e.g. no cost data at all) — that's "no data", not a real zero.
            if (!group.Sums.TryGetValue(column, out var sum))
            {
                cells[column] = TextCell("—");
                continue;
            }

            cells[column] = column switch
            {
                "hours" => new TableCell { Number = sum, Display = ReportFormat.HoursLabel(sum) },
                "labourCost" => new TableCell
                {
                    Number = sum,
                    Display = ReportFormat.Money(sum, group.Currencies.SingleOrDefault() ?? "")
                },
                _ => TextCell("")
            };
        }

        return new TableRow
        {
            Key = $"subtotal:{group.Depth}:{group.Label}:{Guid.NewGuid()}",
            Cells = cells,
            Kind = TableRowKind.GroupSubtotal,
            Depth = group.Depth
        };
    }

    private static decimal? EntryNumericValue(EntryRow row, string column) => column switch
    {
        "hours" => SummaryReportAnalytics.Hours(row.DurationSeconds),
        "labourCost" => row.Cost is null ? null : ReportRounding.Cost(row.Cost.CalculatedCost),
        _ => null
    };

    private static ProseResult EvaluateNote(NoteBlockSpec block)
    {
        var paragraphs = block.Text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        if (paragraphs.Count == 0)
            paragraphs.Add("");

        return new ProseResult
        {
            Id = block.Id,
            Title = block.Title,
            Paragraphs = paragraphs
        };
    }

    private static ProseResult EvaluateNarrative(NarrativeBlockSpec block)
    {
        if (!string.IsNullOrWhiteSpace(block.CachedText))
        {
            return new ProseResult
            {
                Id = block.Id,
                Title = block.Title ?? "Narrative summary",
                Paragraphs = block.CachedText
                    .Replace("\r\n", "\n", StringComparison.Ordinal)
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToList()
            };
        }

        return new ProseResult
        {
            Id = block.Id,
            Title = block.Title ?? "Narrative summary",
            Paragraphs = ["Narrative summaries aren't configured — generate one from the builder."],
            Footnote = "Cached narrative text is empty."
        };
    }

    private static TableCell TextCell(string display) => new() { Display = display };

    /// <summary>Distinct notes for the fan-out dimensions in play, in the order given.</summary>
    private static IEnumerable<string> FanOutNotes(IReadOnlyList<DimensionDefinition> dimensions) =>
        dimensions
            .Where(dimension => dimension.FansOut)
            .Select(dimension => dimension.FanOutNote ?? DimensionCatalog.TagFanOutFootnote)
            .Distinct(StringComparer.Ordinal);

    private static string? JoinFootnotes(params string?[] parts)
    {
        var present = parts.Where(part => !string.IsNullOrWhiteSpace(part)).ToList();
        return present.Count == 0 ? null : string.Join(" ", present);
    }

    private static decimal? EvaluateComputed(ComputedColumnSpec computed, IReadOnlyDictionary<string, decimal?> values)
    {
        var left = values.GetValueOrDefault(computed.Left);
        if (left is null)
            return null;

        // A literal beats a metric reference; the validator guarantees exactly one is set.
        // A missing right operand is "not measurable", not zero — treating it as zero turned
        // Multiply into a silent 0 and Add/Subtract into a value that looked measured.
        var right = computed.RightValue
            ?? (computed.Right is null ? null : values.GetValueOrDefault(computed.Right));
        if (right is null && computed.Operator is not ComputedOperator.PctOfTotal)
            return null;

        return computed.Operator switch
        {
            ComputedOperator.Add => left + right,
            ComputedOperator.Subtract => left - right,
            ComputedOperator.Multiply => left * right,
            ComputedOperator.Divide => right != 0m
                ? Math.Round(left.Value / right!.Value, 4, MidpointRounding.AwayFromZero)
                : null,
            // Needs full-table totals, so CustomReportService fills it in after evaluation.
            ComputedOperator.PctOfTotal => null,
            _ => null
        };
    }

    private static IEnumerable<(string Key, string[] Labels, string Currency, Dictionary<string, decimal?> Values, long SortSeconds)> OrderBreakdown(
        List<(string Key, string[] Labels, string Currency, Dictionary<string, decimal?> Values, long SortSeconds)> rows,
        BreakdownBlockSpec block)
    {
        var key = block.SortKey;
        Func<(string Key, string[] Labels, string Currency, Dictionary<string, decimal?> Values, long SortSeconds), decimal> selector =
            row =>
            {
                if (key is null)
                    return row.SortSeconds;
                return row.Values.GetValueOrDefault(key) ?? 0m;
            };

        return block.SortDescending
            ? rows.OrderByDescending(selector).ThenBy(r => r.Labels[0], StringComparer.OrdinalIgnoreCase)
            : rows.OrderBy(selector).ThenBy(r => r.Labels[0], StringComparer.OrdinalIgnoreCase);
    }

    private static TableRow ToTableRow(
        (string Key, string[] Labels, string Currency, Dictionary<string, decimal?> Values, long SortSeconds) row,
        IReadOnlyList<DimensionDefinition> dimensions,
        IReadOnlyList<MetricDefinition> metrics,
        IReadOnlyList<ComputedColumnSpec> computed,
        IReadOnlyDictionary<string, MetricDefinition> metricsById,
        bool hasMoney)
    {
        var cells = new Dictionary<string, TableCell>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < dimensions.Count; i++)
            cells[dimensions[i].Id] = TextCell(row.Labels[i]);

        if (hasMoney)
            cells["currency"] = TextCell(row.Currency);

        foreach (var metric in metrics)
        {
            var value = row.Values.GetValueOrDefault(metric.Id);
            cells[metric.Id] = new TableCell
            {
                Number = value,
                Display = CustomReportDisplay.Format(value, metric.Unit, row.Currency)
            };
        }

        foreach (var column in computed)
        {
            var value = row.Values.GetValueOrDefault(column.Id);
            cells[column.Id] = new TableCell
            {
                Number = value,
                Display = value is null
                    ? "—"
                    : column.Operator is ComputedOperator.PctOfTotal
                        ? ReportFormat.Percent(value.Value)
                        : FormatComputed(value.Value, column, metricsById, row.Currency)
            };
        }

        return new TableRow { Key = row.Key, Cells = cells };
    }

    private sealed record RowGroup(
        string Key,
        string[] Labels,
        long SortHint,
        List<EntryRow> Rows,
        string? CurrencyCode);

    private static List<RowGroup> GroupRows(
        IReadOnlyList<EntryRow> rows,
        IReadOnlyList<DimensionDefinition> dimensions,
        bool splitByCurrency)
    {
        var map = new Dictionary<string, RowGroup>(StringComparer.Ordinal);

        foreach (var row in rows)
        {
            var keySets = dimensions.Select(d => d.KeysOf(row).ToList()).ToList();
            foreach (var combo in Cartesian(keySets))
            {
                var key = string.Join('\u001f', combo.Select(k => k.Value));
                if (splitByCurrency)
                    key += '\u001f' + row.CurrencyCode;

                if (!map.TryGetValue(key, out var group))
                {
                    group = new RowGroup(
                        key,
                        combo.Select(k => k.Label).ToArray(),
                        combo[0].SortHint,
                        [],
                        splitByCurrency ? row.CurrencyCode : null);
                    map[key] = group;
                }

                group.Rows.Add(row);
            }
        }

        return map.Values.ToList();
    }

    /// <summary>
    /// A computed column inherits its left operand's presentation, so revenue ÷ hours reads as
    /// money and hours × a literal still reads as hours.
    /// </summary>
    private static string FormatComputed(
        decimal value,
        ComputedColumnSpec column,
        IReadOnlyDictionary<string, MetricDefinition> metricsById,
        string currency) =>
        metricsById.TryGetValue(column.Left, out var left)
            ? CustomReportDisplay.Format(value, left.Unit, currency)
            : value.ToString("0.####", CultureInfo.InvariantCulture);

    private static TableColumnType ComputedColumnType(
        ComputedColumnSpec computed,
        IReadOnlyDictionary<string, MetricDefinition> metricsById)
    {
        if (computed.Operator is ComputedOperator.PctOfTotal)
            return TableColumnType.Percent;

        if (metricsById.TryGetValue(computed.Left, out var left))
            return CustomReportDisplay.ToColumnType(left.Unit);

        return TableColumnType.Hours;
    }

    private static IEnumerable<EntryRow> SortEntries(
        IReadOnlyList<EntryRow> rows,
        IReadOnlyList<Application.Common.Models.ReportGroupBy> groupBy)
    {
        IOrderedEnumerable<EntryRow>? ordered = null;
        foreach (var dimension in groupBy)
        {
            ordered = ordered is null
                ? rows.OrderBy(r => EntryGroupKey(r, dimension), StringComparer.OrdinalIgnoreCase)
                : ordered.ThenBy(r => EntryGroupKey(r, dimension), StringComparer.OrdinalIgnoreCase);
        }

        ordered = ordered is null
            ? rows.OrderByDescending(r => r.Date).ThenBy(r => r.UserName, StringComparer.OrdinalIgnoreCase)
            : ordered.ThenByDescending(r => r.Date).ThenBy(r => r.UserName, StringComparer.OrdinalIgnoreCase);

        return ordered.ThenBy(r => r.EntryId);
    }

    private static string EntryGroupKey(EntryRow row, Application.Common.Models.ReportGroupBy groupBy) =>
        groupBy switch
        {
            Application.Common.Models.ReportGroupBy.User => row.UserName,
            Application.Common.Models.ReportGroupBy.Project => row.ProjectLabel,
            Application.Common.Models.ReportGroupBy.Client => row.ClientLabel,
            Application.Common.Models.ReportGroupBy.Task => row.TaskLabel,
            Application.Common.Models.ReportGroupBy.Tag => row.Tags.Count == 0
                ? "(No tags)"
                : string.Join(",", row.Tags.Select(t => t.Label)),
            Application.Common.Models.ReportGroupBy.Billable => row.IsBillable ? "Billable" : "Non-billable",
            Application.Common.Models.ReportGroupBy.Day => row.Date.ToString("yyyy-MM-dd"),
            Application.Common.Models.ReportGroupBy.Week => row.WeekStart.ToString("yyyy-MM-dd"),
            _ => ""
        };

    private static IEnumerable<IReadOnlyList<DimensionKey>> Cartesian(IReadOnlyList<IReadOnlyList<DimensionKey>> sets)
    {
        IEnumerable<IReadOnlyList<DimensionKey>> seed = [Array.Empty<DimensionKey>()];
        foreach (var set in sets)
        {
            seed = seed.SelectMany(
                prefix => set.Select(item => (IReadOnlyList<DimensionKey>)prefix.Append(item).ToArray()));
        }

        return seed;
    }

    private static string? ResolveSingleCurrency(IReadOnlyList<EntryRow> rows)
    {
        var codes = rows.Select(r => r.CurrencyCode).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return codes.Count == 1 ? codes[0] : null;
    }

    private static string ResolveMixedCurrency(IEnumerable<string> currencies)
    {
        var distinct = currencies.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return distinct.Count == 1 ? distinct[0] : MixedCurrencyCode;
    }

    private static long SumSeconds(IEnumerable<EntryRow> rows) =>
        rows.Sum(r => r.DurationSeconds);
}
