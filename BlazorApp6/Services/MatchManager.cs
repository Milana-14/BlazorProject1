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
            try
            {
                matches = MatchFileManager.LoadFromFile(AppConstants.MatchesFilePath);
                history = MatchFileManager.LoadFromFile(AppConstants.HistoryFilePath);
                FileLoadError = null;
            }
            catch (ApplicationException ex)
            {
                matches = new List<Match>();
                history = new List<Match>();
                FileLoadError = ex.Message;
            }
        }

        public Match RequestMatch(Student student1, Student student2)
        {
            Match match = new Match(student1, student2);
            if (!matches.Any(m => m.Id == match.Id))
            {
                matches.Add(match);
                MatchFileManager.SaveToFile(matches);
                return match;
            }
            return null; // to see if this is the correct solution
        }
        public void ConfirmMatch(Match match)
        {
            match.Confirm();
            MatchFileManager.SaveToFile(matches);
        }
        public void RejectMatch(Match match)
        {
            matches.Remove(match);
            match.Reject();
            MatchFileManager.SaveToFile(matches);
        }
        public void CancelMatchRequest(Match match)
        {
            matches.Remove(match);
            MatchFileManager.SaveToFile(matches);
        }
        public void UnpairStudents(Match match)
        {
            matches.Remove(match);
            match.Unpair();
            if (!history.Any(m => m.Id == match.Id)) history.Add(match);
            MatchFileManager.SaveToFile(matches);
            MatchFileManager.SaveToFile(history);
        }
        public List<Match> FindMatchesByStudent(Guid studentId)
        {
            return matches.Where(m => m.Student1Id == studentId || m.Student2Id == studentId).ToList();
        }
        public Match FindMatchById(Guid id)
        {
            return matches.FirstOrDefault(m => m.Id == id);
        }
        public List<Match> GetAllMatches()
        {
            return matches;
        }


        // класс для чтения и записи данных в файл 
        private static class MatchFileManager
        {
            public static void SaveToFile(List<Match> list)
            {
                string line = JsonSerializer.Serialize(list);
                File.WriteAllText(AppConstants.MatchesFilePath, line);
            }

            public static List<Match> LoadFromFile(string filePath)
            {
                if (!File.Exists(filePath))
                {
                    return new List<Match>();
                }

                try
                {
                    string lines = File.ReadAllText(filePath);
                    List<Match> list = JsonSerializer.Deserialize<List<Match>>(lines);
                    return list;
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
