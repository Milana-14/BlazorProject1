using BlazorApp6.Components.Models;
using Npgsql;

namespace BlazorApp6.Services
{
    public class SubjectsManager
    {
        private readonly string connectionString;
        public string? DbError { get; private set; } = string.Empty;
        public SubjectsManager(IConfiguration config)
        {
            connectionString = config.GetConnectionString("DefaultConnection");
        }

        public bool AddSubject(Student student, SubjectEnum newSubject, bool canHelp)
        {
            if (student == null)
            {
                DbError = "Ученик с това ID не е намерен.";
                return false;
            }
            if (student.CanHelpWith.Contains(newSubject) || student.NeedsHelpWith.Contains(newSubject))
            {
                DbError = "Този предмет вече е избран.";
                return false;
            }

            if (canHelp) student.CanHelpWith.Add(newSubject);
            else student.NeedsHelpWith.Add(newSubject);
            AddSubjectToDb(newSubject, canHelp: canHelp, student);
            return true;
        }
        public bool RemoveSubject(Student student, SubjectEnum subjectToRemove, bool canHelp)
        {
            if (student == null)
            {
                DbError = "Ученик с това ID не е намерен.";
                return false;
            }
            if (!student.CanHelpWith.Contains(subjectToRemove) && !student.NeedsHelpWith.Contains(subjectToRemove))
            {
                DbError = "Този предмет не е избран от ученика.";
                return false;
            }

            if (canHelp) student.CanHelpWith.Remove(subjectToRemove);
            else student.NeedsHelpWith.Remove(subjectToRemove);
            RemoveSubjectFromDb(subjectToRemove, canHelp: canHelp, student);
            return true;
        }

        public List<StudentSubject> GetSubjectsByStudent(Student student)
        {
            if (student == null)
            {
                DbError = "Ученик с това ID не е намерен.";
                return new List<StudentSubject>();
            }
            if (!LoadSubjectsForStudentFromDb(out List<StudentSubject> canHelpSubjectsFromDb, out List<StudentSubject> needsHelpSubjectsFromDb, student))
            {
                DbError += "Зареждането на данните за предметите не беше успешно.";
                return new List<StudentSubject>();
            }
            else
            {
                return canHelpSubjectsFromDb.Concat(needsHelpSubjectsFromDb).ToList();
            }
        }

        public List<StudentSubject> GetAllSubjects()
        {
            if (!LoadAllSubjectsFromDb(out List<StudentSubject> subjectsFromDb))
            {
                DbError += "Зареждането на данните за предметите не беше успешно.";
                return new List<StudentSubject>();
            }
            else
            {
                return subjectsFromDb;
            }
        }




        // Работа с база данни
        public bool UpdateStudentSubjects(Student student)
        {
            if (student == null)
            {
                DbError = "Ученик с това ID не е намерен.";
                return false;
            }

            try
            {
                using var connection = new NpgsqlConnection(connectionString);
                connection.Open();

                var currentSubjects = new List<StudentSubject>();
                using (var cmd = new NpgsqlCommand(
                    @"SELECT ""Subject"", ""CanHelp"" FROM ""StudentSubjects"" WHERE ""StudentId""=@StudentId",
                    connection))
                {
                    cmd.Parameters.AddWithValue("@StudentId", student.Id);
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        currentSubjects.Add(new StudentSubject(
                            StudentId: student.Id,
                            Subject: (SubjectEnum)reader.GetInt32(0),
                            CanHelp: reader.GetBoolean(1)
                        ));
                    }
                }

                // За доабавяне
                var toAdd = new List<StudentSubject>();
                foreach (var s in student.CanHelpWith)
                {
                    if (!currentSubjects.Any(cs => cs.Subject == s && cs.CanHelp))
                        toAdd.Add(new StudentSubject(student.Id, s, true));
                }
                foreach (var s in student.NeedsHelpWith)
                {
                    if (!currentSubjects.Any(cs => cs.Subject == s && !cs.CanHelp))
                        toAdd.Add(new StudentSubject(student.Id, s, false));
                }

                foreach (var sub in toAdd)
                {
                    using var cmd = new NpgsqlCommand(
                        @"INSERT INTO ""StudentSubjects"" (""StudentId"", ""Subject"", ""CanHelp"")
                  VALUES (@StudentId, @Subject, @CanHelp)",
                        connection);
                    cmd.Parameters.AddWithValue("@StudentId", student.Id);
                    cmd.Parameters.AddWithValue("@Subject", (int)sub.Subject);
                    cmd.Parameters.AddWithValue("@CanHelp", sub.CanHelp);
                    cmd.ExecuteNonQuery();
                }

                // За изтриване
                var toRemove = currentSubjects
                    .Where(cs => cs.CanHelp
                                 ? !student.CanHelpWith.Contains(cs.Subject)
                                 : !student.NeedsHelpWith.Contains(cs.Subject))
                    .ToList();

                foreach (var sub in toRemove)
                {
                    using var cmd = new NpgsqlCommand(
                        @"DELETE FROM ""StudentSubjects"" 
                  WHERE ""StudentId""=@StudentId AND ""Subject""=@Subject AND ""CanHelp""=@CanHelp",
                        connection);
                    cmd.Parameters.AddWithValue("@StudentId", student.Id);
                    cmd.Parameters.AddWithValue("@Subject", (int)sub.Subject);
                    cmd.Parameters.AddWithValue("@CanHelp", sub.CanHelp);
                    cmd.ExecuteNonQuery();
                }

                return true;
            }
            catch (Exception ex)
            {
                DbError = "Грешка при синхронизацията на предметите: " + ex.Message;
                return false;
            }
        } // Най-доброто нещо, което съм създавала някога (4 часаааа)
        public bool LoadSubjectsForStudentFromDb(out List<StudentSubject> canHelpSubjectsFromDb, out List<StudentSubject> needsHelpSubjectsFrOmDb, Student student)
        {
            canHelpSubjectsFromDb = new();
            needsHelpSubjectsFrOmDb = new();

            try
            {
                using var connection = new NpgsqlConnection(connectionString);
                connection.Open();

                string sql = @"SELECT ""StudentId"", ""Subject"", ""CanHelp"" 
               FROM ""StudentSubjects"" 
               WHERE ""StudentId"" = @StudentId";

                using NpgsqlCommand cmd = new NpgsqlCommand(sql, connection);
                cmd.Parameters.AddWithValue("@StudentId", student.Id);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    StudentSubject subject = new StudentSubject(
                        StudentId: reader.GetGuid(0),
                        Subject: (SubjectEnum)reader.GetInt32(1),
                        CanHelp: reader.GetBoolean(2)
                    );

                    if (subject.CanHelp) canHelpSubjectsFromDb.Add(subject);
                    else needsHelpSubjectsFrOmDb.Add(subject);
                }
                return true;
            }
            catch (Exception ex)
            {
                DbError = "Грешка при зареждането: " + ex.Message;
                return false;
            }
        } 
        public bool LoadAllSubjectsFromDb(out List<StudentSubject> subjectsFromDb)
        {
            subjectsFromDb = new();
            try
            {
                using var connection = new NpgsqlConnection(connectionString);
                connection.Open();
                using NpgsqlCommand cmd = new NpgsqlCommand(@"SELECT ""StudentId"", ""Subject"", ""CanHelp"" FROM ""StudentSubjects""", connection);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    StudentSubject subject = new StudentSubject(
                        StudentId: reader.GetGuid(0),
                        Subject: (SubjectEnum)reader.GetInt32(1),
                        CanHelp: reader.GetBoolean(2)
                    );
                    subjectsFromDb.Add(subject);
                }
                return true;
            }
            catch (Exception ex)
            {
                DbError = "Грешка при зареждането: " + ex.Message;
                return false;
            }
        }
        public bool AddSubjectToDb(SubjectEnum subject, bool canHelp, Student student)
        {
            try
            {
                using (NpgsqlConnection connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();

                    string sql = @"INSERT INTO ""StudentSubjects"" 
                           (""StudentId"", ""Subject"", ""CanHelp"")
                           VALUES (@StudentId, @Subject, @CanHelp)";

                    using NpgsqlCommand cmd = new NpgsqlCommand(sql, connection);
                    cmd.Parameters.AddWithValue("@StudentId", student.Id);
                    cmd.Parameters.AddWithValue("@Subject", (int)subject);
                    cmd.Parameters.AddWithValue("@CanHelp", canHelp);

                    cmd.ExecuteNonQuery();
                }
                return true;
            }
            catch (Exception ex)
            {
                DbError = "Записването на нов предмет в базата не беше успешно." + ex;
                return false;
            }
        } 
        public bool RemoveSubjectFromDb(SubjectEnum subject, bool canHelp, Student student)
        {
            try
            {
                using (NpgsqlConnection connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();

                    string sql = @"DELETE FROM ""StudentSubjects"" 
                                    WHERE ""StudentId""=@StudentId AND ""Subject""=@Subject AND ""CanHelp""=@CanHelp";

                    using NpgsqlCommand cmd = new NpgsqlCommand(sql, connection);
                    cmd.Parameters.AddWithValue("@StudentId", student.Id);
                    cmd.Parameters.AddWithValue("@Subject", (int)subject);
                    cmd.Parameters.AddWithValue("@CanHelp", canHelp);

                    cmd.ExecuteNonQuery();
                }
                return true;
            }
            catch (Exception ex)
            {
                DbError = "Изтриването на предмет от базата не беше успешно." + ex;
                return false;
            }
        }
    }
}