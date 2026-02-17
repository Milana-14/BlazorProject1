using BlazorApp6.Models;
using Microsoft.AspNetCore.Components.Forms;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;

namespace BlazorApp6.Services
{
    // Този клас отговаря за качването, изтриването и предоставянето на URL за аватарите на студентите.
    // Той използва ImageSharp за обработка на изображенията, като ги преоразмерява до максимум 1024x1024 пиксела и ги запазва с качество 85.
    // Когато се качва нов аватар, старият се изтрива (ако не е default.jpg) и се обновява информацията в базата данни чрез StudentManager.  

    public class AvatarManager
    {
        private readonly IWebHostEnvironment env;
        private readonly StudentManager studentManager;
        public AvatarManager(IWebHostEnvironment env, StudentManager studentManager)
        {
            this.env = env;
            this.studentManager = studentManager;
        }

        public async Task<string> UploadAvatar(IBrowserFile avatarFile, Student student)
        {
            string extension = Path.GetExtension(avatarFile.Name);
            string newAvatarName = Guid.NewGuid().ToString() + extension;
            if (extension != ".jpg" && extension != ".jpeg" && extension != ".png")
                throw new InvalidOperationException("Този файлов формат не се поддържа (само .jpg, .jpeg, .png).");

            string avatarsPath = Path.Combine(env.WebRootPath, "avatars");
            string oldFullPath = Path.Combine(avatarsPath, student.AvatarName);

            if (student.AvatarName != null && student.AvatarName != "default.jpg" && File.Exists(oldFullPath))
            {
                File.Delete(oldFullPath);
                student.AvatarName = null;
            }

            string newFullPath = Path.Combine(avatarsPath, newAvatarName);
            await using (var memoryStream = new MemoryStream())
            {
                await avatarFile.OpenReadStream(2 * 1024 * 1024).CopyToAsync(memoryStream);
                memoryStream.Position = 0;

                using (var image = Image.Load(memoryStream))
                {
                    image.Mutate(x => x.Resize(new ResizeOptions
                    {
                        Mode = ResizeMode.Max,
                        Size = new Size(1024, 1024)
                    }));

                    await image.SaveAsync(newFullPath, new JpegEncoder { Quality = 85 });
                }
            }

            student.AvatarName = newAvatarName;
            studentManager.UpdateStudent(student);
            return newAvatarName;
        }
        public string GetAvatarUrl(string? avatarName = null)
        {
            if (string.IsNullOrEmpty(avatarName)) return "/avatars/default.jpg";
            else return $"/avatars/{avatarName}";
        }
        public void DeleteAvatar(Student student)
        {
            string fullPath = Path.Combine(env.WebRootPath, "avatars", student.AvatarName);
            if (student.AvatarName != null && student.AvatarName != "default.jpg" && File.Exists(fullPath))
            {
                File.Delete(fullPath);
                student.AvatarName = null;
            }
            studentManager.UpdateStudent(student);
        }
    }
}
