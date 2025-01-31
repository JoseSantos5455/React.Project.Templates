﻿using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Asp.React.Project.Template.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Asp.React.Project.Template.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IConfiguration _configuration;
    private readonly IOptions<ApiBehaviorOptions> _apiBehaviorOptions;

    public AuthController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, IConfiguration configuration, IOptions<ApiBehaviorOptions> apiBehaviorOptions)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _configuration = configuration;
        _apiBehaviorOptions = apiBehaviorOptions;
    }

    [HttpPost]
    [Route("login")]
    public async Task<ActionResult<JwtData>> Login([FromBody] LoginModel model)
    {
        var user = await _userManager.FindByNameAsync(model.Username) ?? await _userManager.FindByEmailAsync(model.Username);
        if (user != null && await _userManager.CheckPasswordAsync(user, model.Password))
        {
            var token = await GetToken(user);
    
            return Ok(new JwtData()
            {
                Token = new JwtSecurityTokenHandler().WriteToken(token),
                Expiration = token.ValidTo
            });
        }
    
        ModelState.AddModelError("", "Invalid username or password");
        return (ActionResult)_apiBehaviorOptions.Value.InvalidModelStateResponseFactory(ControllerContext);
    }
    
    [HttpPost]
    [Route("register")]
    public async Task<ActionResult<Response>> Register([FromBody] RegisterModel model)
    {
        var userExists = await _userManager.FindByNameAsync(model.Username);
        if (userExists != null) ModelState.AddModelError("", "Username already taken");
        
        userExists = await _userManager.FindByEmailAsync(model.Email);
        if (userExists != null) ModelState.AddModelError("", "Email already taken");

        if (!ModelState.IsValid) return (ActionResult)_apiBehaviorOptions.Value.InvalidModelStateResponseFactory(ControllerContext);

        ApplicationUser user = new()
        {
            Email = model.Email,
            SecurityStamp = Guid.NewGuid().ToString(),
            UserName = model.Username
        };
        var result = await _userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            foreach (var identityError in result.Errors)
            {
                ModelState.AddModelError("", identityError.Description);
            }
            return (ActionResult)_apiBehaviorOptions.Value.InvalidModelStateResponseFactory(ControllerContext);
        }

        return Ok(new Response { Status = "Success", Message = "User created successfully!" });
    }

    [HttpGet("oauth")]
    public async Task<IActionResult> SignIn([FromQuery] string provider)
    {
        // Note: the "provider" parameter corresponds to the external
        // authentication provider choosen by the user agent.
        if (string.IsNullOrWhiteSpace(provider))
        {
            return BadRequest();
        }

        if (!await HttpContext.IsProviderSupportedAsync(provider))
        {
            return BadRequest();
        }

        // Instruct the middleware corresponding to the requested external identity
        // provider to redirect the user agent to its own authorization endpoint.
        // Note: the authenticationScheme parameter must match the value configured in Startup.cs
        return Challenge(new AuthenticationProperties { RedirectUri = "/app" }, provider);
    }
    
    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<ApplicationUser>> CurrentUserInfo()
    {
        var user = HttpContext.GetUser();
        return user;
    }

    private async Task<JwtSecurityToken> GetToken(ApplicationUser user)
    {
        var userRoles = await _userManager.GetRolesAsync(user);

        var authClaims = new List<Claim>
        {
            new(ClaimTypes.Name, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        foreach (var userRole in userRoles)
        {
            authClaims.Add(new Claim(ClaimTypes.Role, userRole));
        }

        return GetToken(authClaims);
    }

    public static JwtSecurityToken GetToken(List<Claim> authClaims)
    {
        var token = new JwtSecurityToken(
            issuer: AuthOptions.ISSUER,
            audience: AuthOptions.AUDIENCE,
            expires: DateTime.Now.AddDays(3),
            claims: authClaims,
            signingCredentials: new SigningCredentials(AuthOptions.GetSymmetricSecurityKey(),
                SecurityAlgorithms.HmacSha256)
        );

        return token;
    }
}