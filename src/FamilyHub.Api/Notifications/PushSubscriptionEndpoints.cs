using System.Security.Claims;
using FamilyHub.Api.Authentication;
using FamilyHub.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace FamilyHub.Api.Notifications;

public static class PushSubscriptionEndpoints
{
    public static RouteGroupBuilder MapPushSubscriptionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/push-subscriptions").RequireAuthorization();
        group.MapGet("/vapid-public-key", GetVapidPublicKey);
        group.MapPost("/", Register);
        group.MapPost("/{pushSubscriptionId:guid}/test", SendTest);
        group.MapDelete("/{pushSubscriptionId:guid}", Delete);
        return group;
    }

    private static IResult GetVapidPublicKey(IConfiguration configuration)
    {
        var publicKey = configuration["Notifications:Vapid:PublicKey"];
        return string.IsNullOrWhiteSpace(publicKey)
            ? Results.Problem("Push notifications are not configured.", statusCode: StatusCodes.Status503ServiceUnavailable)
            : Results.Ok(new VapidPublicKeyResponse(publicKey));
    }

    private static async Task<IResult> Register(
        RegisterPushSubscriptionRequest request,
        ClaimsPrincipal user,
        FamilyHubDbContext db)
    {
        if (!Uri.TryCreate(request.Endpoint, UriKind.Absolute, out var endpoint) ||
            endpoint.Scheme != Uri.UriSchemeHttps ||
            string.IsNullOrWhiteSpace(request.P256dh) ||
            string.IsNullOrWhiteSpace(request.Auth))
        {
            return Results.BadRequest("A valid HTTPS Push subscription is required.");
        }

        if (string.IsNullOrWhiteSpace(request.TimeZoneId))
        {
            return Results.BadRequest("The browser time zone is required.");
        }

        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(request.TimeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return Results.BadRequest("The browser time zone is not recognized.");
        }
        catch (InvalidTimeZoneException)
        {
            return Results.BadRequest("The browser time zone is not valid.");
        }

        var currentMember = await CurrentFamilyMember.FindAsync(user, db);
        if (currentMember is null)
        {
            return Results.NotFound();
        }

        var subscription = await db.BrowserPushSubscriptions.SingleOrDefaultAsync(candidate =>
            candidate.Endpoint == request.Endpoint);
        if (subscription is not null)
        {
            if (subscription.FamilyMemberId != currentMember.Id)
            {
                return Results.Conflict("This Push subscription belongs to another Family Member.");
            }

            subscription.P256dh = request.P256dh;
            subscription.Auth = request.Auth;
            subscription.TimeZoneId = request.TimeZoneId;
            await db.SaveChangesAsync();
            return Results.Ok(ToResponse(subscription));
        }

        subscription = new BrowserPushSubscription
        {
            Id = Guid.NewGuid(),
            FamilyMemberId = currentMember.Id,
            Endpoint = request.Endpoint,
            P256dh = request.P256dh,
            Auth = request.Auth,
            TimeZoneId = request.TimeZoneId,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        db.BrowserPushSubscriptions.Add(subscription);
        await db.SaveChangesAsync();

        return Results.Created($"/api/push-subscriptions/{subscription.Id}", ToResponse(subscription));
    }

    private static async Task<IResult> SendTest(
        Guid pushSubscriptionId,
        ClaimsPrincipal user,
        FamilyHubDbContext db,
        IPushNotificationSender sender,
        CancellationToken cancellationToken)
    {
        var currentMember = await CurrentFamilyMember.FindAsync(user, db);
        if (currentMember is null)
        {
            return Results.NotFound();
        }

        var subscription = await db.BrowserPushSubscriptions.AsNoTracking().SingleOrDefaultAsync(candidate =>
            candidate.Id == pushSubscriptionId &&
            candidate.FamilyMemberId == currentMember.Id,
            cancellationToken);
        if (subscription is null)
        {
            return Results.NotFound();
        }

        await sender.SendAsync(
            subscription,
            new PushNotification(
                "Family Hub notifications are ready",
                "Scheduled Medication reminders can now reach this device.",
                "/medications",
                "family-hub-notifications-ready"),
            cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> Delete(
        Guid pushSubscriptionId,
        ClaimsPrincipal user,
        FamilyHubDbContext db)
    {
        var currentMember = await CurrentFamilyMember.FindAsync(user, db);
        if (currentMember is null)
        {
            return Results.NotFound();
        }

        var subscription = await db.BrowserPushSubscriptions.SingleOrDefaultAsync(candidate =>
            candidate.Id == pushSubscriptionId &&
            candidate.FamilyMemberId == currentMember.Id);
        if (subscription is null)
        {
            return Results.NotFound();
        }

        db.BrowserPushSubscriptions.Remove(subscription);
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    private static PushSubscriptionResponse ToResponse(BrowserPushSubscription subscription) =>
        new(subscription.Id);
}

public record RegisterPushSubscriptionRequest(
    string Endpoint,
    string P256dh,
    string Auth,
    string TimeZoneId);

public record PushSubscriptionResponse(Guid PushSubscriptionId);

public record VapidPublicKeyResponse(string PublicKey);