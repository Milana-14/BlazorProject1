using BlazorApp6.Models;
using Microsoft.AspNetCore.SignalR;
using Npgsql;

namespace BlazorApp6.Services
{
    public class SubjectsManager
    {
        private readonly string connectionString;

        private readonly Dictionary<Guid, List<StudentSubject>> subjectsByStudent = new();

        public string? DbError { get; private set; } = string.Empty;

        public SubjectsManager(IConfiguration config)
        {
            connectionString = config.GetConnectionString("DefaultConnection");

            if (!LoadAllSubjectsFromDb(out var all))
            {
                DbError = "Зареждането на предметите не беше успешно.";
                return;
            }

            foreach (var s in all)
            {
                if (!subjectsByStudent.TryGetValue(s.StudentId, out var list))
                    subjectsByStudent[s.StudentId] = list = new List<StudentSubject>();

                list.Add(s);
            }
        }


        public bool AddSubject(Student student, SubjectEnum newSubject, bool canHelp)
        {
            if (student == null)
            {
                DbError = "Този ученик не е намерен.";
                return false;
            }

            var list = GetOrCreateStudentList(student.Id);

            if (list.Any(s => s.Subject == newSubject))
            {
                DbError = "Този предмет вече е избран.";
                return false;
            }

            var newEntry = new StudentSubject(student.Id, newSubject, canHelp);

            list.Add(newEntry);

            if (canHelp) student.CanHelpWith.Add(newSubject);
            else student.NeedsHelpWith.Add(newSubject);

            return AddSubjectToDb(newEntry);
        }

        public bool RemoveSubject(Student student, SubjectEnum subjectToRemove, bool canHelp)
        {
            if (student == null)
            {
                DbError = "Този ученик не е намерен.";
                return false;
            }

            if (!subjectsByStudent.TryGetValue(student.Id, out var list))
                return false;

            var existing = list.FirstOrDefault(s => s.Subject == subjectToRemove && s.CanHelp == canHelp);
            if (existing == null)
            {
                DbError = "Този предмет не е избран.";
                return false;
            }

            list.Remove(existing);

            if (canHelp) student.CanHelpWith.Remove(subjectToRemove);
            else student.NeedsHelpWith.Remove(subjectToRemove);

            return RemoveSubjectFromDb(existing);
        }

        public List<StudentSubject> GetSubjectsByStudent(Student student)
        {
            if (student == null)
            {
                DbError = "Този ученик не е намерен.";
                return new List<StudentSubject>();
            }

            if (subjectsByStudent.TryGetValue(student.Id, out var list))
                return list;

            return new List<StudentSubject>();
        }

        public List<StudentSubject> GetAllSubjects()
        {
            return subjectsByStudent.Values.SelectMany(v => v).ToList();
        }


        public bool UpdateStudentSubjects(Student student)
        {
            if (student == null)
                return false;

            try
            {
                using var connection = CreateConnection();

                using var deleteCmd = new NpgsqlCommand(
                    @"DELETE FROM ""StudentSubjects"" WHERE ""StudentId""=@StudentId", connection);
                deleteCmd.Parameters.AddWithValue("@StudentId", student.Id);
                deleteCmd.ExecuteNonQuery();

                var newList = new List<StudentSubject>();

                foreach (var s in student.CanHelpWith)
                    newList.Add(new StudentSubject(student.Id, s, true));

                foreach (var s in student.NeedsHelpWith)
                    newList.Add(new StudentSubject(student.Id, s, false));

                foreach (var sub in newList)
                    InsertSubject(connection, sub);

                subjectsByStudent[student.Id] = newList;

                return true;
            }
            catch (Exception ex)
            {
                DbError = ex.Message;
                return false;
            }
        }


        private bool LoadAllSubjectsFromDb(out List<StudentSubject> subjectsFromDb)
        {
            subjectsFromDb = new();

            try
            {
                using var connection = CreateConnection();

                using var cmd = new NpgsqlCommand(
                    @"SELECT ""StudentId"", ""Subject"", ""CanHelp"" FROM ""StudentSubjects""", connection);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    subjectsFromDb.Add(new StudentSubject(
                        reader.GetGuid(0),
                        (SubjectEnum)reader.GetInt32(1),
                        reader.GetBoolean(2)));
                }

                return true;
            }
            catch (Exception ex)
            {
                DbError = ex.Message;
                return false;
            }
        }

        private bool AddSubjectToDb(StudentSubject subject)
        {
            try
            {
                using var connection = CreateConnection();
                InsertSubject(connection, subject);
                return true;
            }
            catch (Exception ex)
            {
                DbError = ex.Message;
                return false;
            }
        }

        private bool RemoveSubjectFromDb(StudentSubject subject)
        {
            try
            {
                using var connection = CreateConnection();

                using var cmd = new NpgsqlCommand(
                    @"DELETE FROM ""StudentSubjects""
                      WHERE ""StudentId""=@StudentId AND ""Subject""=@Subject AND ""CanHelp""=@CanHelp", connection);

                cmd.Parameters.Add("@StudentId", NpgsqlTypes.NpgsqlDbType.Uuid).Value = subject.StudentId;
                cmd.Parameters.Add("@Subject", NpgsqlTypes.NpgsqlDbType.Integer).Value = (int)subject.Subject;
                cmd.Parameters.Add("@CanHelp", NpgsqlTypes.NpgsqlDbType.Boolean).Value = subject.CanHelp;

                cmd.Prepare();
                cmd.ExecuteNonQuery();
                return true;
            }
            catch (Exception ex)
            {
                DbError = ex.Message;
                return false;
            }
        }

        private static void InsertSubject(NpgsqlConnection connection, StudentSubject sub)
        {
            using var cmd = new NpgsqlCommand(
                @"INSERT INTO ""StudentSubjects"" (""StudentId"", ""Subject"", ""CanHelp"")
                  VALUES (@StudentId,@Subject,@CanHelp)", connection);

            cmd.Parameters.Add("@StudentId", NpgsqlTypes.NpgsqlDbType.Uuid).Value = sub.StudentId;
            cmd.Parameters.Add("@Subject", NpgsqlTypes.NpgsqlDbType.Integer).Value = (int)sub.Subject;
            cmd.Parameters.Add("@CanHelp", NpgsqlTypes.NpgsqlDbType.Boolean).Value = sub.CanHelp;

            cmd.Prepare();
            cmd.ExecuteNonQuery();
        }

        private NpgsqlConnection CreateConnection()
        {
            var c = new NpgsqlConnection(connectionString);
            c.Open();
            return c;
        }

        private List<StudentSubject> GetOrCreateStudentList(Guid id)
        {
            if (!subjectsByStudent.TryGetValue(id, out var list))
                subjectsByStudent[id] = list = new List<StudentSubject>();

            return list;
        }
    }
}