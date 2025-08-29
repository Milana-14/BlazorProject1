using BlazorApp6.Models;

namespace BlazorApp6.Services
{
    public class AppState // За да се следи текущия логнат потребител
    {
        public Student? CurrentUser {  get; set; }
    }
}
