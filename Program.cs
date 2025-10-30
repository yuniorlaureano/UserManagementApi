using System.ComponentModel.DataAnnotations;
using UserManagementAPI.Models;
using UserManagementAPI.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Basic auth simulation middleware — protects /users endpoints
app.UseMiddleware<BasicAuthMiddleware>();

// In-memory store (no database)
var users = new List<User>
{
    new User { Id = Guid.NewGuid(), FirstName = "Alice", LastName = "Smith", Age = 30 },
    new User { Id = Guid.NewGuid(), FirstName = "Bob", LastName = "Jones", Age = 25 }
};
var usersLock = new object();

// CRUD endpoints

// List all users
app.MapGet("/users", () =>
{
    lock (usersLock)
    {
        return Results.Ok(users);
    }
});

// Get single user by id
app.MapGet("/users/{id:guid}", (Guid id) =>
{
    lock (usersLock)
    {
        var user = users.FirstOrDefault(u => u.Id == id);
        return user is null ? Results.NotFound() : Results.Ok(user);
    }
});

// Create a new user
app.MapPost("/users", (UserDto dto) =>
{
    if (string.IsNullOrWhiteSpace(dto.FirstName) || string.IsNullOrWhiteSpace(dto.LastName) || dto.Age < 0)
        return Results.BadRequest(new { error = "Invalid user data." });

    var user = new User
    {
        Id = Guid.NewGuid(),
        FirstName = dto.FirstName,
        LastName = dto.LastName,
        Age = dto.Age
    };

    lock (usersLock)
    {
        users.Add(user);
    }

    return Results.Created($"/users/{user.Id}", user);
});

// Update an existing user
app.MapPut("/users/{id:guid}", (Guid id, UserDto dto) =>
{
    if (string.IsNullOrWhiteSpace(dto.FirstName) || string.IsNullOrWhiteSpace(dto.LastName) || dto.Age < 0)
        return Results.BadRequest(new { error = "Invalid user data." });

    lock (usersLock)
    {
        var existing = users.FirstOrDefault(u => u.Id == id);
        if (existing is null) return Results.NotFound();

        existing.FirstName = dto.FirstName;
        existing.LastName = dto.LastName;
        existing.Age = dto.Age;
        return Results.NoContent();
    }
});

// Delete a user
app.MapDelete("/users/{id:guid}", (Guid id) =>
{
    lock (usersLock)
    {
        var user = users.FirstOrDefault(u => u.Id == id);
        if (user is null) return Results.NotFound();
        users.Remove(user);
        return Results.NoContent();
    }
});

app.Run();

// DTO for create/update to avoid client-specified IDs
public record UserDto([Required] string FirstName, [Required] string LastName, int Age);
