using System.ComponentModel.DataAnnotations;
namespace BlazorApp6.Components.Models;

public abstract class User
{
    public abstract string Name { get; }
    public abstract string SecName { get; }
    public abstract int Age { get; }
    public abstract string PhoneNumber { get; }
    public abstract string Username { get; }
    public abstract string Password { get; }

    public abstract void PrintInfo();
}