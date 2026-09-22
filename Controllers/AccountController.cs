using EStore.Models;
using EStore.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace EStore.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
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
        // PROTECTED PROFILE PAGE
        // ==================================================
        [Authorize]
        [HttpGet]
        public IActionResult Profile()
        {
            return View();
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