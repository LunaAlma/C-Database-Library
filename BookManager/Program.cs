// See https://aka.ms/new-console-template for more information
using System;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Globalization;
using Npgsql;

class Program
{
    // You can set PG_CONN as an environment variable, e.g.:
    // export PG_CONN="Host=localhost;Username=postgres;Password=123;Database=booksdb"
    static string ConnectionString =>
        Environment.GetEnvironmentVariable("PG_CONN")
        ?? "Host=localhost;Username=postgres;Password=123;Database=booksdb";

    static async Task Main()
    {
        Console.WriteLine("Book Manager CLI. Type 'help' for commands.");
        await EnsureTableExists();

        while (true)
        {
            Console.Write("> ");
            var line = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(line)) continue;

            var parts = SplitArgs(line);
            if (parts.Length == 0) continue;

            var cmd = parts[0].ToLowerInvariant();
            try
            {
                switch (cmd)
                {
                    case "add":
                        if (parts.Length != 4)
                        {
                            Console.WriteLine("Usage: add \"Title\" \"Author\" YYYY-MM-DD");
                            Console.WriteLine("Note: If the title or author contains spaces, wrap them in quotes. Example:");
                            Console.WriteLine("  add \"The Three Musketeers\" \"Alexandre Dumas\" 1844-03-14");
                            break;
                        }

                        string title = parts[1];
                        string author = parts[2];
                        string dateStr = parts[3];
                        if (!DateTime.TryParseExact(dateStr, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                                DateTimeStyles.None, out var releaseDate))
                        {
                            Console.WriteLine("Release date must be in format YYYY-MM-DD.");
                            break;
                        }

                        await AddBook(title, author, releaseDate);
                        break;

                    case "remove":
                        if (parts.Length < 2)
                        {
                            Console.WriteLine("Usage: remove \"Title\"");
                            break;
                        }

                        string titleToRemove = parts[1];
                        await RemoveBookByTitle(titleToRemove);
                        break;

                    case "list":
                        await ListBooks();
                        break;

                    case "help":
                        PrintHelp();
                        break;

                    case "exit":
                    case "quit":
                        return;

                    default:
                        Console.WriteLine($"Unknown command: {cmd}. Type 'help' to see available commands.");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
    }

    static void PrintHelp()
    {
        Console.WriteLine(@"Commands:
  add ""Title"" ""Author"" YYYY-MM-DD   Adds a book. Example: add ""Ali Babanın Çiftliği"" ""Bülent Ersoy"" 1937-09-21
  remove ""Title""                     Removes the book with that title.
  list                               Lists all books.
  help                               Shows this message.
  exit / quit                        Exit the program.");
    }

    static string[] SplitArgs(string commandLine)
    {
        var matches = Regex.Matches(commandLine, @"[\""].+?[\""]|\S+");
        var args = new string[matches.Count];
        for (int i = 0; i < matches.Count; i++)
        {
            args[i] = matches[i].Value.Trim('"');
        }

        return args;
    }

    static async Task EnsureTableExists()
    {
        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();
        string ddl = @"
CREATE TABLE IF NOT EXISTS books (
  id SERIAL PRIMARY KEY,
  title TEXT NOT NULL UNIQUE,
  author TEXT NOT NULL,
  release_date DATE
);";
        await using var cmd = new NpgsqlCommand(ddl, conn);
        await cmd.ExecuteNonQueryAsync();
    }

    static async Task AddBook(string title, string author, DateTime releaseDate)
    {
        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();

        const string sql = @"
INSERT INTO books (title, author, release_date)
VALUES (@title, @author, @release_date);
";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("title", title);
        cmd.Parameters.AddWithValue("author", author);
        cmd.Parameters.AddWithValue("release_date", releaseDate);

        try
        {
            int affected = await cmd.ExecuteNonQueryAsync();
            if (affected == 1)
                Console.WriteLine($"Added book: \"{title}\" by {author} ({releaseDate:yyyy-MM-dd})");
        }
        catch (PostgresException pgEx) when (pgEx.SqlState == "23505") // unique_violation
        {
            Console.WriteLine($"A book with the title \"{title}\" already exists.");
        }
    }

    static async Task RemoveBookByTitle(string title)
    {
        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();

        const string sql = @"
DELETE FROM books
WHERE title ILIKE @title;
";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("title", title); // ILIKE with parameter is fine

        int affected = await cmd.ExecuteNonQueryAsync();
        if (affected > 0)
            Console.WriteLine($"Removed book titled \"{title}\".");
        else
            Console.WriteLine($"No book found with title \"{title}\".");
    }

    static async Task ListBooks()
    {
        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();

        const string sql = "SELECT title, author, release_date FROM books ORDER BY title;";
        await using var cmd = new NpgsqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();

        Console.WriteLine("Books in database:");
        bool any = false;
        while (await reader.ReadAsync())
        {
            any = true;
            var title = reader.GetString(0);
            var author = reader.GetString(1);
            string date = reader.IsDBNull(2) ? "N/A" : reader.GetDateTime(2).ToString("yyyy-MM-dd");
            Console.WriteLine($"  \"{title}\" by {author}, released: {date}");
        }

        if (!any)
            Console.WriteLine("  (none)");
    }
}