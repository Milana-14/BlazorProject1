using BlazorApp6.Components.Models;
using System.Text.Json;

namespace BlazorApp6.Services
{
    public class MatchManager
    {
        private List<Match> matches;
        private List<Match> history;

        public string? FileLoadError { get; private set; }
        public MatchManager()
        {
            matches = MatchFileManager.LoadFromFile();

            try
            {
                matches = MatchFileManager.LoadFromFile();
                FileLoadError = null;
            }
            catch (ApplicationException ex)
            {
                matches = new List<Match>();
                FileLoadError = ex.Message;
            }
        }

        public Match RequestMatch(Student student1, Student student2)
        {
            Match match = new Match(student1, student2);
            matches.Add(match);
            MatchFileManager.SaveToFile(matches);
            return match;
        }
        public void ConfirmMatch(Match match)
        {
            match.Confirm();
            MatchFileManager.SaveToFile(matches);
        }
        public void RejectMatch(Match match)
        {
            match.Reject();
            history.Add(match);
            MatchFileManager.SaveToFile(matches);
        }
        public void CancelMatch(Match match)
        {
            matches.Remove(match);
            MatchFileManager.SaveToFile(matches);
        }
        public void UnpairStudents(Match match)
        {
            matches.Remove(match);
            MatchFileManager.SaveToFile(matches);
        }
        public List<Match> FindMatchesByStudent(Guid studentId)
        {
            return matches.Where(m => m.Student1Id == studentId || m.Student2Id == studentId).ToList();
        }
        public Match FindMatchById(Guid id)
        {
            return matches.Where(m => m.Id == id).FirstOrDefault();
        }
        public List<Match> GetAllMatches()
        {
            return matches;
        }


        // класс для чтения и записи данных в файл 
        private static class MatchFileManager
        {
            public static void SaveToFile(List<Match> matches)
            {
                string line = JsonSerializer.Serialize(matches);
                File.WriteAllText(AppConstants.MatchesFilePath, line);
            }

            public static List<Match> LoadFromFile()
            {
                if (!File.Exists(AppConstants.MatchesFilePath))
                {
                    return new List<Match>();
                }

                try
                {
                    string lines = File.ReadAllText(AppConstants.MatchesFilePath);
                    List<Match> matches = JsonSerializer.Deserialize<List<Match>>(lines);
                    return matches;
                }
                catch (Exception ex)
                {
                    // Можно логировать ошибку (если есть логгер), а пользователю показать:
                    throw new ApplicationException("Зареждането на данните не беше успешно. Опитайте отново по-късно.");
                }
            }
        }
    }
}
