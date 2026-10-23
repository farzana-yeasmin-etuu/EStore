using EStore.Models;
using EStore.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using EStore.Data;
using Microsoft.EntityFrameworkCore;


namespace EStore.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly EStore.Services.IEmailService _email;
        private readonly ApplicationDbContext _context;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            EStore.Services.IEmailService email,
            ApplicationDbContext context)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _email = email;
            _context = context;
        }



        // ==================================================
        // REGISTER - GET
        // ==================================================
        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }


        // ==================================================
        // REGISTER - POST
        // ==================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(
            RegisterViewModel model)
        {
            // Check form validation
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Create new ApplicationUser
            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                FullName = model.FullName
            };

            // Create user in database
            var result = await _userManager.CreateAsync(
                user,
                model.Password
            );

            // If user creation is successful
            if (result.Succeeded)
            {
                // Give new user the Customer role
                var roleResult = await _userManager.AddToRoleAsync(
                    user,
                    "Customer"
                );

                // Check if role assignment was successful
                if (!roleResult.Succeeded)
                {
                    foreach (var error in roleResult.Errors)
                    {
                        ModelState.AddModelError(
                            "",
                            error.Description
                        );
                    }

                    return View(model);
                }

                // Automatically login after registration
                await _signInManager.SignInAsync(
                    user,
                    isPersistent: false
                );

                await _email.SendWelcomeAsync(
    user.Email!,
    user.FullName
);

                return RedirectToAction(
                    "Index",
                    "Home"
                );
            }

            // Show registration errors
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(
                    "",
                    error.Description
                );
            }

            return View(model);
        }


        // ==================================================
        // LOGIN - GET
        // ==================================================
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl;

            return View();
        }


        // ==================================================
        // LOGIN - POST
        // ==================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(
            LoginViewModel model,
            string? returnUrl = null)
        {
            // Check form validation
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Check email and password
            var result = await _signInManager.PasswordSignInAsync(
                model.Email,
                model.Password,
                model.RememberMe,
                lockoutOnFailure: false
            );

            // Login successful
            if (result.Succeeded)
            {
                // If user originally tried to access
                // a protected page, send them back there.
                if (!string.IsNullOrEmpty(returnUrl) &&
                    Url.IsLocalUrl(returnUrl))
                {
                    return Redirect(returnUrl);
                }

                // Otherwise go to Home page
                return RedirectToAction(
                    "Index",
                    "Home"
                );
            }

            // Login failed
            ModelState.AddModelError(
                "",
                "Invalid email or password."
            );

            return View(model);
        }


        // ==================================================
        // PROFILE PAGE
        // ==================================================
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return RedirectToAction(nameof(Login));
            }

            return View(await BuildProfileAsync(user));
        }


        // ==================================================
        // UPDATE PERSONAL INFORMATION + DEFAULT ADDRESS
        // ==================================================
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(
            [Bind(Prefix = "Profile")] UpdateProfileViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return RedirectToAction(nameof(Login));
            }

            if (!ModelState.IsValid)
            {
                var vm = await BuildProfileAsync(user);
                vm.Profile = model;

                return View("Profile", vm);
            }

            user.FullName = model.FullName.Trim();
            user.PhoneNumber = Clean(model.PhoneNumber);
            user.Address = Clean(model.Address);
            user.City = Clean(model.City);
            user.PostCode = Clean(model.PostCode);

            var result = await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError("Profile.FullName", error.Description);
                }

                var vm = await BuildProfileAsync(user);
                vm.Profile = model;

                return View("Profile", vm);
            }

            TempData["ProfileSuccess"] = "Your profile has been updated.";

            return RedirectToAction(nameof(Profile));
        }


        // ==================================================
        // CHANGE PASSWORD
        // ==================================================
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(
            [Bind(Prefix = "Password")] ChangePasswordViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return RedirectToAction(nameof(Login));
            }

            if (!ModelState.IsValid)
            {
                return View("Profile", await BuildProfileAsync(user));
            }

            var result = await _userManager.ChangePasswordAsync(
                user,
                model.CurrentPassword,
                model.NewPassword);

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    // Wrong current password shows under that field, the rest under the new password
                    var field = error.Code == "PasswordMismatch"
                        ? "Password.CurrentPassword"
                        : "Password.NewPassword";

                    ModelState.AddModelError(field, error.Description);
                }

                return View("Profile", await BuildProfileAsync(user));
            }

            // Changing a password changes the security stamp. Refresh so the user stays logged in.
            await _signInManager.RefreshSignInAsync(user);

            TempData["PasswordSuccess"] = "Your password has been changed.";

            return RedirectToAction(nameof(Profile));
        }


        // ==================================================
        // HELPERS
        // ==================================================

        private static string? Clean(string? text) =>
            string.IsNullOrWhiteSpace(text) ? null : text.Trim();

        // Bangladesh is UTC+6 all year (no daylight saving)
        private static DateTime ToBd(DateTime utc) =>
            DateTime.SpecifyKind(utc, DateTimeKind.Utc).AddHours(6);

        private async Task<ProfileViewModel> BuildProfileAsync(ApplicationUser user)
        {
            var myOrders = _context.Orders
                .AsNoTracking()
                .Where(o => o.UserId == user.Id);

            var recent = await myOrders
                .OrderByDescending(o => o.CreatedAt)
                .Take(3)
                .Select(o => new
                {
                    o.Id,
                    o.OrderNumber,
                    o.CreatedAt,
                    o.TotalAmount,
                    o.OrderStatus
                })
                .ToListAsync();

            // "RK" from "Rahim Khan"
            var initials = string.Concat(
                (user.FullName ?? "")
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Take(2)
                    .Select(part => char.ToUpper(part[0])));

            return new ProfileViewModel
            {
                FullName = user.FullName ?? "",
                Email = user.Email ?? "",
                Initials = string.IsNullOrEmpty(initials) ? "?" : initials,

                TotalOrders = await myOrders.CountAsync(),

                ActiveOrders = await myOrders.CountAsync(o =>
                    o.OrderStatus != "Delivered" && o.OrderStatus != "Cancelled"),

                TotalSpent = await myOrders
                    .Where(o => o.OrderStatus != "Cancelled")
                    .SumAsync(o => (decimal?)o.TotalAmount) ?? 0,

                WishlistCount = await _context.WishlistItems
                    .CountAsync(w => w.UserId == user.Id),

                RecentOrders = recent
                    .Select(o => new ProfileOrderRow
                    {
                        Id = o.Id,
                        OrderNumber = o.OrderNumber,
                        Date = ToBd(o.CreatedAt).ToString("dd MMM yyyy"),
                        TotalAmount = o.TotalAmount,
                        OrderStatus = o.OrderStatus
                    })
                    .ToList(),

                Profile = new UpdateProfileViewModel
                {
                    FullName = user.FullName ?? "",
                    PhoneNumber = user.PhoneNumber,
                    Address = user.Address,
                    City = user.City,
                    PostCode = user.PostCode
                }
            };
        }


        // ==================================================
        // LOGOUT
        // ==================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            // Sign out current user
            await _signInManager.SignOutAsync();

            // Return to Home page
            return RedirectToAction(
                "Index",
                "Home"
            );
        }
    }
}