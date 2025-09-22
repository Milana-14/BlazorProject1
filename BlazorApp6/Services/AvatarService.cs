using BlazorApp6.Models;
using Microsoft.AspNetCore.Components.Forms;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;

namespace BlazorApp6.Services
{
    public class AvatarService
    {
        private readonly IWebHostEnvironment env;
        private readonly StudentManager studentManager;
        public AvatarService(IWebHostEnvironment env, StudentManager studentManager)
        {
            this.env = env;
            this.studentManager = studentManager;
        }

        public string UploadAvatar(IBrowserFile avatarFile, Student student)
        {
            string extension = System.IO.Path.GetExtension(avatarFile.Name);
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
            using (var memoryStream = new MemoryStream())
            {
                avatarFile.OpenReadStream(2 * 1024 * 1024).CopyTo(memoryStream);
                memoryStream.Position = 0;

                using (var image = Image.Load(memoryStream))
                {
                    image.Mutate(x => x.Resize(new ResizeOptions
                    {
                        Mode = ResizeMode.Max,
                        Size = new Size(1024, 1024)
                    }));

                    image.Save(newFullPath, new JpegEncoder { Quality = 85 });
                }
            }

            student.AvatarName = newAvatarName;
            studentManager.UpdateStudent(student);

            return newAvatarName;
        }
        public string GetAvatarUrl(string? avatarName = null)
        {
            if (avatarName == null) return "/avatars/default.jpg";
            else return $"/avatars/{avatarName}";
        }
        public void DeleteAvatar(Student student)
        {
            string avatarsPath = Path.Combine(env.WebRootPath, "avatars");
            string fullPath = Path.Combine(avatarsPath, student.AvatarName);
            if (student.AvatarName != null && student.AvatarName != "default.jpg" && File.Exists(fullPath))
            {
                File.Delete(fullPath);
                student.AvatarName = null;
            }
            studentManager.UpdateStudent(student);
        }
    }
}
