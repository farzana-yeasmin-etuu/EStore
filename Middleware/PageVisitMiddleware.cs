using System.Security.Claims;
using EStore.Data;
using EStore.Models;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.EntityFrameworkCore;

namespace EStore.Middleware
{
    public class PageVisitMiddleware
    {
        private readonly RequestDelegate _next;

        public PageVisitMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(
            HttpContext context,
            ApplicationDbContext db)
        {
            var endpoint = context.GetEndpoint();

            var actionDescriptor =
                endpoint?.Metadata
                    .GetMetadata<ControllerActionDescriptor>();

            bool shouldTrack =
                HttpMethods.IsGet(context.Request.Method) &&
                actionDescriptor != null &&
                actionDescriptor.ControllerName != "Admin";

            string? visitorId = null;
            string? userId = null;
            string? pageName = null;
            string? url = null;

            if (shouldTrack)
            {
                const string cookieName = "EverSpringVisitorId";

                if (!context.Request.Cookies.TryGetValue(
                        cookieName, out visitorId) ||
                    !Guid.TryParse(visitorId, out _))
                {
                    visitorId = Guid.NewGuid().ToString();

                    context.Response.Cookies.Append(
                        cookieName,
                        visitorId,
                        new CookieOptions
                        {
                            HttpOnly = true,
                            Secure = context.Request.IsHttps,
                            SameSite = SameSiteMode.Lax,
                            Expires = DateTimeOffset.UtcNow.AddYears(1),
                            IsEssential = true
                        });
                }

                userId = context.User
                    .FindFirstValue(ClaimTypes.NameIdentifier);

                pageName =
                    $"{actionDescriptor!.ControllerName}/" +
                    $"{actionDescriptor.ActionName}";

                url = context.Request.Path.Value ?? "/";
            }

            // Execute the actual page request.
            await _next(context);

            // Save only successful page responses.
            if (!shouldTrack ||
                context.Response.StatusCode != StatusCodes.Status200OK)
            {
                return;
            }

            var visit = new PageVisit
            {
                UserId = userId,
                VisitorId = visitorId,
                PageName = pageName!,
                Url = url!,
                VisitedAt = DateTime.UtcNow
            };

            db.PageVisits.Add(visit);

            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                db.Entry(visit).State = EntityState.Detached;
            }
        }
    }
}