using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using ReeTrack.Application.Common.Models;
using ReeTrack.Domain.Entities;
using ReeTrack.Domain.Enums;
using ReeTrack.Infrastructure.Persistence;
using ReeTrack.Infrastructure.Timesheets;
using ReeTrack.IntegrationTests.Support;
using Xunit;

namespace ReeTrack.IntegrationTests.Invoices;

public class InvoiceEndpointsTests
{
    private static DateOnly CurrentWeek => TimesheetWeek.ToWeekStart(DateTime.UtcNow);

    [Fact]
    public async Task Generate_Anonymous_ReturnsUnauthorized()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/invoices/generate", new
        {
            query = new
            {
                clientIds = new[] { Guid.NewGuid() },
                from = CurrentWeek,
                to = CurrentWeek.AddDays(6)
            }
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Generate_AsMember_ReturnsForbidden()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedMemberAsync();
        var client = factory.CreateAuthenticatedClient(token);

        var response = await client.PostAsJsonAsync("/api/invoices/generate", new
        {
            query = new
            {
                clientIds = new[] { Guid.NewGuid() },
                from = CurrentWeek,
                to = CurrentWeek.AddDays(6)
            }
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Generate_AsAdmin_CreatesDraftWithHourlyAndFixedFeeLines()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (admin, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);
        var monday = CurrentWeek;
        Guid clientId;

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var customer = new Client { Name = "Acme" };
            db.Clients.Add(customer);
            await db.SaveChangesAsync();
            clientId = customer.Id;

            var hourly = new Project
            {
                ClientId = customer.Id,
                CreatedByUserId = admin.Id,
                Name = "Hourly Work",
                Status = ProjectStatus.Active,
                CurrencyCode = "EUR",
                HourlyRate = 50m,
                TimeEstimateHours = 1m
            };
            var fixedFee = new Project
            {
                ClientId = customer.Id,
                CreatedByUserId = admin.Id,
                Name = "Fixed Work",
                Status = ProjectStatus.Active,
                CurrencyCode = "EUR",
                FixedFeeAmount = 1000m,
                TimeEstimateHours = 5m
            };
            db.Projects.AddRange(hourly, fixedFee);
            await db.SaveChangesAsync();

            var started = monday.ToDateTime(new TimeOnly(9, 0), DateTimeKind.Utc);
            db.TimeEntries.AddRange(
                new TimeEntry
                {
                    UserId = admin.Id,
                    ClientId = customer.Id,
                    ProjectId = hourly.Id,
                    IsBillable = true,
                    Mode = TimeEntryMode.Manual,
                    StartedAtUtc = started,
                    EndedAtUtc = started.AddHours(2),
                    DurationSeconds = 7200,
                    Status = TimeEntryStatus.Confirmed
                },
                new TimeEntry
                {
                    UserId = admin.Id,
                    ClientId = customer.Id,
                    ProjectId = fixedFee.Id,
                    IsBillable = true,
                    Mode = TimeEntryMode.Manual,
                    StartedAtUtc = started.AddHours(3),
                    EndedAtUtc = started.AddHours(8),
                    DurationSeconds = 18000,
                    Status = TimeEntryStatus.Confirmed
                });
            await db.SaveChangesAsync();
        }

        var response = await client.PostAsJsonAsync("/api/invoices/generate", new
        {
            query = new
            {
                clientIds = new[] { clientId },
                projectIds = Array.Empty<Guid>(),
                from = monday,
                to = monday.AddDays(6)
            }
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var invoice = await response.Content.ReadFromJsonAsync<InvoiceResponse>();
        Assert.NotNull(invoice);
        Assert.Equal("Acme", invoice.ClientName);
        Assert.Equal("EUR", invoice.CurrencyCode);
        Assert.Equal("Draft", invoice.Status);
        Assert.Equal(1100m, invoice.Subtotal); // 2h × 50 + 1000 fixed
        Assert.Equal(2, invoice.LineItems.Count);
        Assert.Contains(invoice.LineItems, line => line.BillingModel == "Hourly" && line.Amount == 100m);
        Assert.Contains(invoice.LineItems, line => line.BillingModel == "FixedFee" && line.Amount == 1000m);
        Assert.Contains(invoice.LineItems, line => line.Description.Contains("estimate", StringComparison.OrdinalIgnoreCase));

        var listed = await client.GetFromJsonAsync<PagedResult<InvoiceResponse>>("/api/invoices");
        Assert.NotNull(listed);
        Assert.Contains(listed.Items, item => item.Id == invoice.Id);

        var detail = await client.GetFromJsonAsync<InvoiceResponse>($"/api/invoices/{invoice.Id}");
        Assert.NotNull(detail);
        Assert.Equal(invoice.Number, detail.Number);
        Assert.Equal(2, detail.LineItems.Count);

        var pdf = await client.GetAsync($"/api/invoices/{invoice.Id}/pdf");
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
        Assert.Contains("attachment", pdf.Content.Headers.ContentDisposition?.DispositionType ?? "");
        var bytes = await pdf.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 100);
        Assert.Equal("%PDF"u8.ToArray(), bytes.Take(4).ToArray());
    }

    [Fact]
    public async Task ExportPdf_UnknownInvoice_ReturnsNotFound()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);

        var response = await client.GetAsync($"/api/invoices/{Guid.NewGuid()}/pdf");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetInvoice_WithNewerInvoiceExists_ReturnsNewerInvoiceDetails()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (admin, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);
        var monday = CurrentWeek;
        Guid clientId;

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var customer = new Client { Name = "Acme Newer Test" };
            db.Clients.Add(customer);
            await db.SaveChangesAsync();
            clientId = customer.Id;

            var hourly = new Project
            {
                ClientId = customer.Id,
                CreatedByUserId = admin.Id,
                Name = "Hourly Project",
                Status = ProjectStatus.Active,
                CurrencyCode = "EUR",
                HourlyRate = 100m,
                TimeEstimateHours = 10m
            };
            db.Projects.Add(hourly);
            await db.SaveChangesAsync();

            var started = monday.ToDateTime(new TimeOnly(9, 0), DateTimeKind.Utc);
            db.TimeEntries.Add(new TimeEntry
            {
                UserId = admin.Id,
                ClientId = customer.Id,
                ProjectId = hourly.Id,
                IsBillable = true,
                Mode = TimeEntryMode.Manual,
                StartedAtUtc = started,
                EndedAtUtc = started.AddHours(2),
                DurationSeconds = 7200,
                Status = TimeEntryStatus.Confirmed
            });
            await db.SaveChangesAsync();
        }

        // Generate first invoice
        var res1 = await client.PostAsJsonAsync("/api/invoices/generate", new
        {
            query = new
            {
                clientIds = new[] { clientId },
                projectIds = Array.Empty<Guid>(),
                from = monday,
                to = monday.AddDays(6)
            }
        });
        Assert.Equal(HttpStatusCode.OK, res1.StatusCode);
        var inv1 = await res1.Content.ReadFromJsonAsync<InvoiceResponse>();
        Assert.NotNull(inv1);
        Assert.Null(inv1.NewerInvoiceId);
        Assert.Null(inv1.NewerInvoiceNumber);

        // Generate second invoice
        var res2 = await client.PostAsJsonAsync("/api/invoices/generate", new
        {
            query = new
            {
                clientIds = new[] { clientId },
                projectIds = Array.Empty<Guid>(),
                from = monday,
                to = monday.AddDays(6)
            }
        });
        Assert.Equal(HttpStatusCode.OK, res2.StatusCode);
        var inv2 = await res2.Content.ReadFromJsonAsync<InvoiceResponse>();
        Assert.NotNull(inv2);

        // Fetch first invoice again and verify NewerInvoice fields
        var detail1 = await client.GetFromJsonAsync<InvoiceResponse>($"/api/invoices/{inv1.Id}");
        Assert.NotNull(detail1);
        Assert.Equal(inv2.Id, detail1.NewerInvoiceId);
        Assert.Equal(inv2.Number, detail1.NewerInvoiceNumber);
    }

    [Fact]
    public async Task Delete_AsMember_ReturnsForbidden()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedMemberAsync();
        var client = factory.CreateAuthenticatedClient(token);

        var response = await client.DeleteAsync($"/api/invoices/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Delete_UnknownInvoice_ReturnsNotFound()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);

        var response = await client.DeleteAsync($"/api/invoices/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_AsAdmin_SoftDeletesAndRemovesFromList()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (admin, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);
        Guid invoiceId;

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var customer = new Client { Name = "Delete Me" };
            db.Clients.Add(customer);
            await db.SaveChangesAsync();

            var invoice = new Invoice
            {
                Number = "INV-DEL-1",
                ClientId = customer.Id,
                ClientName = customer.Name,
                CurrencyCode = "EUR",
                PeriodFrom = CurrentWeek,
                PeriodTo = CurrentWeek.AddDays(6),
                Subtotal = 100m,
                Status = InvoiceStatus.Draft,
                GeneratedByUserId = admin.Id,
                LineItems =
                [
                    new InvoiceLineItem
                    {
                        ProjectId = null,
                        Description = "Test line",
                        BillingModel = InvoiceLineBillingModel.Hourly,
                        Quantity = 2m,
                        UnitPrice = 50m,
                        Amount = 100m,
                        SortOrder = 1
                    }
                ]
            };
            db.Invoices.Add(invoice);
            await db.SaveChangesAsync();
            invoiceId = invoice.Id;
        }

        var delete = await client.DeleteAsync($"/api/invoices/{invoiceId}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var listed = await client.GetFromJsonAsync<PagedResult<InvoiceResponse>>("/api/invoices");
        Assert.NotNull(listed);
        Assert.DoesNotContain(listed.Items, item => item.Id == invoiceId);

        var detail = await client.GetAsync($"/api/invoices/{invoiceId}");
        Assert.Equal(HttpStatusCode.NotFound, detail.StatusCode);

        var pdf = await client.GetAsync($"/api/invoices/{invoiceId}/pdf");
        Assert.Equal(HttpStatusCode.NotFound, pdf.StatusCode);
    }

    [Fact]
    public async Task MarkPaid_AsMember_ReturnsForbidden()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedMemberAsync();
        var client = factory.CreateAuthenticatedClient(token);

        var response = await client.PostAsync($"/api/invoices/{Guid.NewGuid()}/mark-paid", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task MarkPaid_AsAdmin_MarksInvoiceAsPaid()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (admin, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);
        var (_, invoiceId) = await SeedDraftInvoiceAsync(factory, admin.Id);

        var response = await client.PostAsync($"/api/invoices/{invoiceId}/mark-paid", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var paid = await response.Content.ReadFromJsonAsync<InvoiceResponse>();
        Assert.NotNull(paid);
        Assert.Equal("Paid", paid.Status);

        var listed = await client.GetFromJsonAsync<PagedResult<InvoiceResponse>>("/api/invoices");
        Assert.NotNull(listed);
        var listedInvoice = Assert.Single(listed.Items, item => item.Id == invoiceId);
        Assert.Equal("Paid", listedInvoice.Status);

        var pdf = await client.GetAsync($"/api/invoices/{invoiceId}/pdf");
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);

        var delete = await client.DeleteAsync($"/api/invoices/{invoiceId}");
        Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);
    }

    [Fact]
    public async Task MarkPaid_AlreadyPaid_ReturnsConflict()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (admin, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);
        var (_, invoiceId) = await SeedDraftInvoiceAsync(factory, admin.Id);

        var first = await client.PostAsync($"/api/invoices/{invoiceId}/mark-paid", null);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await client.PostAsync($"/api/invoices/{invoiceId}/mark-paid", null);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task MarkPaid_UnknownInvoice_ReturnsNotFound()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);

        var response = await client.PostAsync($"/api/invoices/{Guid.NewGuid()}/mark-paid", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task<(Guid ClientId, Guid InvoiceId)> SeedDraftInvoiceAsync(
        ReeTrackWebApplicationFactory factory,
        Guid adminId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var customer = new Client { Name = $"Client {Guid.NewGuid().ToString("N")[..8]}" };
        db.Clients.Add(customer);
        await db.SaveChangesAsync();

        var invoice = new Invoice
        {
            Number = $"INV-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
            ClientId = customer.Id,
            ClientName = customer.Name,
            CurrencyCode = "EUR",
            PeriodFrom = CurrentWeek,
            PeriodTo = CurrentWeek.AddDays(6),
            Subtotal = 100m,
            Status = InvoiceStatus.Draft,
            GeneratedByUserId = adminId,
            LineItems =
            [
                new InvoiceLineItem
                {
                    Description = "Test line",
                    BillingModel = InvoiceLineBillingModel.Hourly,
                    Quantity = 2m,
                    UnitPrice = 50m,
                    Amount = 100m,
                    SortOrder = 1
                }
            ]
        };
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();
        return (customer.Id, invoice.Id);
    }

    private sealed class InvoiceResponse
    {
        public Guid Id { get; init; }
        public string Number { get; init; } = "";
        public string ClientName { get; init; } = "";
        public string CurrencyCode { get; init; } = "";
        public string Status { get; init; } = "";
        public decimal Subtotal { get; init; }
        public Guid? NewerInvoiceId { get; init; }
        public string? NewerInvoiceNumber { get; init; }
        public IReadOnlyList<LineResponse> LineItems { get; init; } = [];
    }

    private sealed class LineResponse
    {
        public string Description { get; init; } = "";
        public string BillingModel { get; init; } = "";
        public decimal Amount { get; init; }
    }
}
