using System;
using System.IO;
using Microsoft.Data.Sqlite;

var dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Cardex", "collection.db");
using var conn = new SqliteConnection($"Data Source={dbPath}");
conn.Open();
var cmd = conn.CreateCommand();
cmd.CommandText = "DELETE FROM UnlockedAchievements";
var rows = cmd.ExecuteNonQuery();
Console.WriteLine($"Done: {rows} achievement(s) deleted.");
