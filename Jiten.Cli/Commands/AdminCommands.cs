using Jiten.Core;
using Jiten.Core.Data.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Jiten.Cli.Commands;

public class AdminCommands(CliContext context)
{
    public async Task RegisterAdmin(string email, string username, string password)
    {
        var services = new ServiceCollection();
        services.AddDbContext<UserDbContext>(options => options.UseNpgsql(context.Configuration.GetConnectionString("JitenDatabase"),
                                                                          o =>
                                                                          {
                                                                              o.UseQuerySplittingBehavior(QuerySplittingBehavior
                                                                                  .SplitQuery);
                                                                          }));

        services.AddLogging(configure => configure.AddConsole());

        var roleName = nameof(UserRole.Administrator);

        services.AddIdentity<User, IdentityRole>(options =>
                {
                    options.Password.RequireDigit = true;
                    options.Password.RequireLowercase = true;
                    options.Password.RequireUppercase = true;
                    options.Password.RequireNonAlphanumeric = false;
                    options.Password.RequiredLength = 10;

                    options.User.RequireUniqueEmail = true;
                })
                .AddEntityFrameworkStores<UserDbContext>()
                .AddDefaultTokenProviders();

        var serviceProvider = services.BuildServiceProvider();

        using (var scope = serviceProvider.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var dbContext = scope.ServiceProvider.GetRequiredService<UserDbContext>();

            Console.WriteLine($"Attempting to create user: {username} ({email}) with role: {roleName}");

            if (!await roleManager.RoleExistsAsync(roleName))
            {
                Console.WriteLine($"Role '{roleName}' does not exist. Creating it...");
                var roleResult = await roleManager.CreateAsync(new IdentityRole(roleName));
                if (!roleResult.Succeeded)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Failed to create role '{roleName}':");
                    foreach (var error in roleResult.Errors)
                    {
                        Console.WriteLine($"- {error.Description}");
                    }

                    Console.ResetColor();
                    return;
                }

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Role '{roleName}' created successfully.");
                Console.ResetColor();
            }

            var existingUserByUsername = await userManager.FindByNameAsync(username);
            if (existingUserByUsername != null)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"User with username '{username}' already exists.");
                Console.ResetColor();
                return;
            }

            var existingUserByEmail = await userManager.FindByEmailAsync(email);
            if (existingUserByEmail != null)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"User with email '{email}' already exists.");
                Console.ResetColor();
                return;
            }

            var user = new User() { UserName = username, Email = email, EmailConfirmed = true, SecurityStamp = Guid.NewGuid().ToString() };

            var createUserResult = await userManager.CreateAsync(user, password);

            if (!createUserResult.Succeeded)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Failed to create user '{username}':");
                foreach (var error in createUserResult.Errors)
                {
                    Console.WriteLine($"- {error.Description}");
                }

                Console.ResetColor();
                return;
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"User '{username}' created successfully with ID: {user.Id}");
            Console.ResetColor();

            var addToRoleResult = await userManager.AddToRoleAsync(user, roleName);
            if (!addToRoleResult.Succeeded)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Failed to add user '{username}' to role '{roleName}':");
                foreach (var error in addToRoleResult.Errors)
                {
                    Console.WriteLine($"- {error.Description}");
                }

                Console.ResetColor();
                return;
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"User '{username}' successfully added to role '{roleName}'.");
            Console.ResetColor();
        }
    }
}
