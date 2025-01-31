﻿using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Asp.React.Project.Template.Controllers;
using Asp.React.Project.Template.Models;
using JwtRegisteredClaimNames = Microsoft.IdentityModel.JsonWebTokens.JwtRegisteredClaimNames;

namespace Asp.React.Project.Template.Auth;

public static class AuthUtils
{
    public static async Task OnTicketReceived(TicketReceivedContext ctx)
    {
        var userManager = ctx.HttpContext.RequestServices.GetService<UserManager<ApplicationUser>>();

        var email = ctx.Principal.Claims.First(c => c.Type == ClaimTypes.Email).Value;
        var name = email.Split('@')[0];

        var user = await userManager.FindByEmailAsync(email);
        if (user == null)
        {
            var userExists = await userManager.FindByNameAsync(name);
            if (userExists != null) name = Guid.NewGuid().ToString();

            user = new()
            {
                Email = email,
                SecurityStamp = Guid.NewGuid().ToString(),
                UserName = name,
                EmailConfirmed = true
            };
            var result = await userManager.CreateAsync(user);
        }
        else
        {
            if (!user.EmailConfirmed)
            {
                user.EmailConfirmed = true;
                await userManager.UpdateAsync(user);
            }
        }

        var authClaims = new List<Claim>
        {
            new(ClaimTypes.Name, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        var token = AuthController.GetToken(authClaims);

        ctx.Response.WriteAsJsonAsync(new JwtData()
        {
            Token = new JwtSecurityTokenHandler().WriteToken(token),
            Expiration = token.ValidTo
        });
        ctx.HandleResponse();
    }
}