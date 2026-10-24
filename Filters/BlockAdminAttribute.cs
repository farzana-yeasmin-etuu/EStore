using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace EStore.Filters
{
    // Admin accounts manage the shop, they do not buy from it.
    // Put [BlockAdmin] on any customer-only action.
    public class BlockAdminAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var http = context.HttpContext;

            if (!http.User.IsInRole("Admin"))
            {
                return;
            }

            if (HttpMethods.IsGet(http.Request.Method))
            {
                // A page: send the admin back to the dashboard with a message
                if (context.Controller is Controller controller)
                {
                    controller.TempData["AdminNotice"] =
                        "Admin accounts cannot place orders. Please use a customer account to test shopping.";
                }

                context.Result = new RedirectToActionResult("Index", "Admin", null);
            }
            else
            {
                // A background request (for example placing an order): refuse it
                context.Result = new JsonResult(new
                {
                    success = false,
                    message = "Admin accounts cannot place orders."
                })
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };
            }
        }
    }
}