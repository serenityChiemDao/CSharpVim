using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

class MiniVim
{
    static readonly List<string> Lines = new() { "" };
    static int x, y, scroll;
    static bool insertMode, dirty;
    static string fileName = "";
    static string message = "Press i to insert, :w to save, :q to quit.";
    static string command = "";

    static void Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.CursorVisible = false;

        if (args.Length > 0)
        {
            fileName = args[0];
            if (File.Exists(fileName))
            {
                Lines.Clear();
                Lines.AddRange(File.ReadAllLines(fileName));
                if (Lines.Count == 0) Lines.Add("");
            }
        }

        try
        {
            while (true)
            {
                Draw();
                var key = Console.ReadKey(true);

                if (insertMode)
                {
                    HandleInsert(key);
                }
                else
                {
                    if (HandleNormal(key)) break;
                }
            }
        }
        finally
        {
            Console.ResetColor();
            Console.CursorVisible = true;
            Console.Write("\x1b[2J\x1b[H");
        }
    }

    static void HandleInsert(ConsoleKeyInfo key)
    {
        if (key.Key == ConsoleKey.Escape)
        {
            insertMode = false;
            message = "-- NORMAL --";
            return;
        }

        if (key.Key == ConsoleKey.Enter)
        {
            string rest = Lines[y].Substring(x);
            Lines[y] = Lines[y].Substring(0, x);
            Lines.Insert(y + 1, rest);
            y++;
            x = 0;
            dirty = true;
        }
        else if (key.Key == ConsoleKey.Backspace)
        {
            if (x > 0)
            {
                Lines[y] = Lines[y].Remove(x - 1, 1);
                x--;
                dirty = true;
            }
            else if (y > 0)
            {
                x = Lines[y - 1].Length;
                Lines[y - 1] += Lines[y];
                Lines.RemoveAt(y);
                y--;
                dirty = true;
            }
        }
        else if (key.Key == ConsoleKey.Delete)
        {
            if (x < Lines[y].Length)
            {
                Lines[y] = Lines[y].Remove(x, 1);
                dirty = true;
            }
            else if (y < Lines.Count - 1)
            {
                Lines[y] += Lines[y + 1];
                Lines.RemoveAt(y + 1);
                dirty = true;
            }
        }
        else if (key.Key == ConsoleKey.LeftArrow)
        {
            MoveLeft();
        }
        else if (key.Key == ConsoleKey.RightArrow)
        {
            if (x < Lines[y].Length) x++;
        }
        else if (key.Key == ConsoleKey.UpArrow)
        {
            MoveVertical(-1);
        }
        else if (key.Key == ConsoleKey.DownArrow)
        {
            MoveVertical(1);
        }
        else if (!char.IsControl(key.KeyChar))
        {
            Lines[y] = Lines[y].Insert(x, key.KeyChar.ToString());
            x++;
            dirty = true;
        }
    }

    // Returns true when the editor should exit.
    static bool HandleNormal(ConsoleKeyInfo key)
    {
        switch (key.Key)
        {
            case ConsoleKey.LeftArrow: MoveLeft(); break;
            case ConsoleKey.RightArrow: MoveRight(); break;
            case ConsoleKey.UpArrow: MoveVertical(-1); break;
            case ConsoleKey.DownArrow: MoveVertical(1); break;
            case ConsoleKey.Backspace: MoveLeft(); break;
            case ConsoleKey.Enter: break;
            default:
                switch (key.KeyChar)
                {
                    case 'h': MoveLeft(); break;
                    case 'l': MoveRight(); break;
                    case 'j': MoveVertical(1); break;
                    case 'k': MoveVertical(-1); break;
                    case '0': x = 0; break;
                    case '$': x = Math.Max(0, Lines[y].Length - 1); break;
                    case 'i':
                        insertMode = true;
                        message = "-- INSERT --";
                        break;
                    case 'a':
                        x = Math.Min(Lines[y].Length, x + 1);
                        insertMode = true;
                        message = "-- INSERT --";
                        break;
                    case 'o':
                        Lines.Insert(y + 1, "");
                        y++;
                        x = 0;
                        dirty = true;
                        insertMode = true;
                        message = "-- INSERT --";
                        break;
                    case 'x':
                        if (x < Lines[y].Length)
                        {
                            Lines[y] = Lines[y].Remove(x, 1);
                            dirty = true;
                        }
                        break;
                    case ':':
                        return RunCommand();
                }
                break;
        }

        x = Math.Clamp(x, 0, Math.Max(0, Lines[y].Length - 1));
        return false;
    }

    static bool RunCommand()
    {
        command = "";
        while (true)
        {
            Draw();
            Console.SetCursorPosition(0, Math.Max(0, Console.WindowHeight - 1));
            Console.Write(":" + command + " ");
            var key = Console.ReadKey(true);

            if (key.Key == ConsoleKey.Escape)
            {
                message = "Command cancelled.";
                return false;
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (command.Length > 0) command = command[..^1];
                continue;
            }

            if (key.Key != ConsoleKey.Enter)
            {
                if (!char.IsControl(key.KeyChar)) command += key.KeyChar;
                continue;
            }

            string[] parts = command.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            string cmd = parts.Length > 0 ? parts[0] : "";
            string? argument = parts.Length > 1 ? parts[1] : null;

            switch (cmd)
            {
                case "w":
                    if (!string.IsNullOrWhiteSpace(argument)) fileName = argument;
                    if (string.IsNullOrWhiteSpace(fileName))
                    {
                        message = "No file name. Use :w filename";
                    }
                    else
                    {
                        Save();
                    }
                    return false;

                case "q":
                    if (dirty)
                    {
                        message = "Unsaved changes! Use :q! to discard or :wq to save.";
                        return false;
                    }
                    return true;

                case "q!":
                    return true;

                case "wq":
                    if (!string.IsNullOrWhiteSpace(argument)) fileName = argument;
                    if (string.IsNullOrWhiteSpace(fileName))
                    {
                        message = "No file name. Use :wq filename";
                        return false;
                    }
                    Save();
                    return true;

                default:
                    message = "Unknown command: " + cmd;
                    return false;
            }
        }
    }

    static void Save()
    {
        try
        {
            File.WriteAllText(fileName, string.Join(Environment.NewLine, Lines),
                new UTF8Encoding(false));
            dirty = false;
            message = $"Saved {fileName}";
        }
        catch (Exception ex)
        {
            message = "Save failed: " + ex.Message;
        }
    }

    static void MoveLeft()
    {
        if (x > 0) x--;
        else if (y > 0) { y--; x = Lines[y].Length; }
    }

    static void MoveRight()
    {
        if (x < Lines[y].Length - 1) x++;
        else if (y < Lines.Count - 1) { y++; x = 0; }
    }

    static void MoveVertical(int direction)
    {
        y = Math.Clamp(y + direction, 0, Lines.Count - 1);
        x = Math.Min(x, Math.Max(0, Lines[y].Length - 1));
    }

    static void Draw()
    {
        int width = Math.Max(10, Console.WindowWidth);
        int height = Math.Max(5, Console.WindowHeight);
        int textHeight = height - 2;

        if (y < scroll) scroll = y;
        if (y >= scroll + textHeight) scroll = y - textHeight + 1;

        var output = new StringBuilder();
        output.Append("\x1b[?25l\x1b[2J\x1b[H");

        for (int row = 0; row < textHeight; row++)
        {
            int lineIndex = scroll + row;
            if (lineIndex < Lines.Count)
            {
                string line = Lines[lineIndex];
                string number = (lineIndex + 1).ToString().PadLeft(4) + " ";
                output.Append("\x1b[90m").Append(number).Append("\x1b[0m");
                int available = Math.Max(0, width - 5);
                output.Append(line.Length > available ? line[..available] : line);
            }
            else
            {
                output.Append("\x1b[90m~\x1b[0m");
            }
            output.Append("\x1b[K\r\n");
        }

        string mode = insertMode ? "-- INSERT --" : "-- NORMAL --";
        string name = string.IsNullOrEmpty(fileName) ? "[No Name]" : fileName;
        string status = $"{name}{(dirty ? " [+]" : "")}  |  {mode}  |  {y + 1}:{x + 1}";
        output.Append("\x1b[7m").Append(status.PadRight(width)[..width]).Append("\x1b[0m\r\n");
        output.Append(message.Length > width ? message[..width] : message);

        int screenX = Math.Clamp(x + 5, 0, width - 1);
        int screenY = Math.Clamp(y - scroll, 0, textHeight - 1);
        output.Append($"\x1b[{screenY + 1};{screenX + 1}H\x1b[?25h");

        Console.Write(output.ToString());
    }
}
