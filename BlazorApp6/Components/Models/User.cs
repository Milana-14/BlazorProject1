using System.ComponentModel.DataAnnotations;
namespace BlazorApp6.Components.Models;

public abstract class User
{
    public abstract string Name { get; set; }
    public abstract string SecName { get; set; }
    public abstract int Age { get; set; }
    public abstract string PhoneNumber { get; set; }
    public abstract string Username { get; }
    public abstract string Password { get; }

    public abstract void PrintInfo();
}