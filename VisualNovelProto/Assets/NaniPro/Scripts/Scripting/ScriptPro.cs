
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace NaniPro.Scripting
{
    // Execution result of a script command.
    public struct CommandResult
    {
        public bool managedIndex;                   // true if command already updated ip
        public System.Collections.IEnumerator routine; // coroutine to run
    }

    // Command interface
    public interface ICommand
    {
        CommandResult Execute(NaniPro.Managers.ScriptPlayerPro player, ref int ip);
    }

    // Parser + script container
    public class ScriptPro
    {
        public List<ICommand> Commands { get; private set; } = new List<ICommand>();
        private Dictionary<string, int> _labels = new Dictionary<string, int>();

        public int GetLabelIndex(string label)
        {
            int idx;
            return _labels.TryGetValue(label, out idx) ? idx : -1;
        }

        public static ScriptPro Parse(string text)
        {
            var s = new ScriptPro();
            var lines = text.Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("//")) continue;

                // Labels: # Label
                if (line.StartsWith("#"))
                {
                    var label = line.Substring(1).Trim();
                    s._labels[label] = s.Commands.Count;
                    s.Commands.Add(new Label(label));
                    continue;
                }

                // Choice: * "Text" -> Label [if expr]
                if (line.StartsWith("*"))
                {
                    var m = Regex.Match(line, @"\*\s*""(?<text>.+?)""\s*->\s*(?<label>\w+)(\s*if\s*(?<cond>.+))?");
                    if (m.Success)
                    {
                        s.Commands.Add(new Choice(m.Groups["text"].Value, m.Groups["label"].Value, m.Groups["cond"].Value));
                        continue;
                    }
                }

                // Commands starting with @
                if (line.StartsWith("@"))
                {
                    var cmd = line.Substring(1).Trim();

                    // @set a = 1
                    if (cmd.StartsWith("set "))
                    {
                        s.Commands.Add(new Set(cmd.Substring(4).Trim()));
                        continue;
                    }

                    // @goto Label
                    if (cmd.StartsWith("goto "))
                    {
                        s.Commands.Add(new Goto(cmd.Substring(5).Trim()));
                        continue;
                    }

                    // @back path [fade:sec]
                    if (cmd.StartsWith("back "))
                    {
                        var rest = cmd.Substring(5).Trim();
                        float fade = ExtractFloat(rest, "fade");
                        var path = rest.Split(' ')[0];
                        s.Commands.Add(new Back(path, fade));
                        continue;
                    }

                    // @char NAME show [appearance:SpriteName] [at:left|center|right] [fade:sec]
                    // @char NAME hide [fade:sec]
                    if (cmd.StartsWith("char "))
                    {
                        var rest = cmd.Substring(5).Trim();
                        var parts = rest.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 2 && parts[1] == "show")
                        {
                            string name = parts[0];
                            string appearance = ExtractValue(rest, "appearance");
                            string atStr = ExtractValue(rest, "at");
                            var at = NaniPro.Managers.AnchorPos.center;
                            if (atStr == "left") at = NaniPro.Managers.AnchorPos.left;
                            else if (atStr == "right") at = NaniPro.Managers.AnchorPos.right;
                            float fade = ExtractFloat(rest, "fade");
                            s.Commands.Add(new CharShow(name, at, appearance, fade));
                            continue;
                        }
                        if (parts.Length >= 2 && parts[1] == "hide")
                        {
                            string name = parts[0];
                            float fade = ExtractFloat(rest, "fade");
                            s.Commands.Add(new CharHide(name, fade));
                            continue;
                        }
                    }

                    // @move NAME to:left|center|right time:0.5
                    if (cmd.StartsWith("move "))
                    {
                        var rest = cmd.Substring(5).Trim();
                        var name = rest.Split(' ')[0];
                        string to = ExtractValue(rest, "to");
                        var pos = NaniPro.Managers.AnchorPos.center;
                        if (to == "left") pos = NaniPro.Managers.AnchorPos.left;
                        else if (to == "right") pos = NaniPro.Managers.AnchorPos.right;
                        float time = ExtractFloat(rest, "time", 0.3f);
                        s.Commands.Add(new Move(name, pos, time));
                        continue;
                    }

                    // @if / @elseif / @else / @endif
                    if (cmd.StartsWith("if "))
                    {
                        s.Commands.Add(new If(cmd.Substring(3).Trim()));
                        continue;
                    }
                    if (cmd.StartsWith("elseif "))
                    {
                        s.Commands.Add(new ElseIf(cmd.Substring(7).Trim()));
                        continue;
                    }
                    if (cmd == "else")
                    {
                        s.Commands.Add(new Else());
                        continue;
                    }
                    if (cmd == "endif")
                    {
                        s.Commands.Add(new EndIf());
                        continue;
                    }

                    // @lang ko|en (no-op placeholder: stores code to variables)
                    if (cmd.StartsWith("lang "))
                    {
                        s.Commands.Add(new Lang(cmd.Substring(5).Trim()));
                        continue;
                    }
                }

                // Dialogue: NAME: "text"  or  "text"
                {
                    var mNamed = Regex.Match(line, @"^(?<name>[^:]+):\s*""(?<text>.*)""$");
                    var mNarra = Regex.Match(line, @"^""(?<text>.*)""$");
                    if (mNamed.Success)
                    {
                        s.Commands.Add(new Say(mNamed.Groups["name"].Value.Trim(), mNamed.Groups["text"].Value));
                        continue;
                    }
                    if (mNarra.Success)
                    {
                        s.Commands.Add(new Say(null, mNarra.Groups["text"].Value));
                        continue;
                    }
                }

                Debug.LogWarning("[NaniPro] Unparsed line: " + line);
            }
            return s;
        }

        private static string ExtractValue(string src, string key)
        {
            var m = Regex.Match(src, key + @":([A-Za-z0-9_\.]+)");
            return m.Success ? m.Groups[1].Value : null;
        }
        private static float ExtractFloat(string src, string key, float def = 0f)
        {
            var m = Regex.Match(src, key + @":([0-9]*\.?[0-9]+)");
            float f;
            if (m.Success && float.TryParse(m.Groups[1].Value, out f)) return f;
            return def;
        }
    }

    // ===== Commands =====

    public class Label : ICommand
    {
        public string name;
        public Label(string n) { name = n; }
        public CommandResult Execute(NaniPro.Managers.ScriptPlayerPro player, ref int ip) { return default(CommandResult); }
    }

    public class Back : ICommand
    {
        public string path; public float fade;
        public Back(string p, float f) { path = p; fade = f; }
        public CommandResult Execute(NaniPro.Managers.ScriptPlayerPro player, ref int ip)
        {
            return new CommandResult { routine = player.backgrounds.SetBackground(path, fade) };
        }
    }

    public class CharShow : ICommand
    {
        public string name, appearance; public NaniPro.Managers.AnchorPos at; public float fade;
        public CharShow(string n, NaniPro.Managers.AnchorPos a, string app, float f) { name = n; at = a; appearance = app; fade = f; }
        public CommandResult Execute(NaniPro.Managers.ScriptPlayerPro player, ref int ip)
        {
            return new CommandResult { routine = player.characters.Show(name, at, appearance, fade) };
        }
    }

    public class CharHide : ICommand
    {
        public string name; public float fade;
        public CharHide(string n, float f) { name = n; fade = f; }
        public CommandResult Execute(NaniPro.Managers.ScriptPlayerPro player, ref int ip)
        {
            return new CommandResult { routine = player.characters.Hide(name, fade) };
        }
    }

    public class Move : ICommand
    {
        public string name; public NaniPro.Managers.AnchorPos to; public float time;
        public Move(string n, NaniPro.Managers.AnchorPos t, float tm) { name = n; to = t; time = tm; }
        public CommandResult Execute(NaniPro.Managers.ScriptPlayerPro player, ref int ip)
        {
            return new CommandResult { routine = player.characters.Move(name, to, time) };
        }
    }

    public class Say : ICommand
    {
        public string author; public string text;
        public Say(string a, string t) { author = a; text = t; }
        public CommandResult Execute(NaniPro.Managers.ScriptPlayerPro player, ref int ip)
        {
            return new CommandResult { routine = player.printer.PrintLine(author, text, key => player.vars.Get(key)?.ToString()) };
        }
    }

    public class Set : ICommand
    {
        public string expr;
        public Set(string e) { expr = e; }
        public CommandResult Execute(NaniPro.Managers.ScriptPlayerPro player, ref int ip)
        {
            var m = Regex.Match(expr, @"^\s*([A-Za-z_]\w*)\s*=\s*(.+)\s*$");
            if (!m.Success) { Debug.LogWarning("[NaniPro] Bad set: " + expr); return default(CommandResult); }
            var key = m.Groups[1].Value;
            var val = m.Groups[2].Value.Trim();

            object boxed;
            if (val.StartsWith("\"") && val.EndsWith("\"")) boxed = val.Substring(1, val.Length - 2);
            else if (bool.TryParse(val, out var b)) boxed = b;
            else if (double.TryParse(val, out var d)) boxed = d;
            else boxed = val;
            player.vars.Set(key, boxed);
            return default(CommandResult);
        }
    }

    public class Goto : ICommand
    {
        public string label;
        public Goto(string l) { label = l; }
        public CommandResult Execute(NaniPro.Managers.ScriptPlayerPro player, ref int ip)
        {
            var res = new CommandResult { managedIndex = true };
            int target = player.ResolveLabel(label);
            if (target < 0) Debug.LogError("[NaniPro] Label not found: " + label);
            else player.DeferredJumpIndex = target;
            return res;
        }
    }

    public class Choice : ICommand
    {
        public string text, target, cond;
        public Choice(string t, string to, string c) { text = t; target = to; cond = c; }

        public CommandResult Execute(NaniPro.Managers.ScriptPlayerPro player, ref int ip)
        {
            // Batch consecutive choices
            var options = new List<(string text, string label, string cond)>();
            var cmds = player.CurrentScript.Commands;
            int j = ip;
            while (j < cmds.Count && cmds[j] is Choice)
            {
                var c = (Choice)cmds[j];
                options.Add((c.text, c.target, c.cond));
                j++;
            }

            // Filter by condition
            var filtered = new List<(string text, string label)>();
            for (int k = 0; k < options.Count; k++)
            {
                var o = options[k];
                if (string.IsNullOrEmpty(o.cond) || player.EvalBool(o.cond))
                    filtered.Add((o.text, o.label));
            }

            if (filtered.Count == 0)
            {
                ip = j;
                return new CommandResult { managedIndex = true };
            }

            var res = new CommandResult { managedIndex = true, routine = RunChoices(player, filtered) };
            ip = j;
            return res;
        }

        private System.Collections.IEnumerator RunChoices(NaniPro.Managers.ScriptPlayerPro p, List<(string text, string label)> opts)
        {
            var labels = new List<string>();
            for (int i = 0; i < opts.Count; i++) labels.Add(opts[i].text);
            yield return p.choices.ShowChoices(labels);
            int picked = Mathf.Clamp(p.choices.LastSelectedIndex, 0, opts.Count - 1);
            int target = p.ResolveLabel(opts[picked].label);
            if (target < 0) Debug.LogError("[NaniPro] Choice label not found: " + opts[picked].label);
            else p.DeferredJumpIndex = target;
        }
    }

    public class If : ICommand
    {
        public string cond;
        public If(string c) { cond = c; }
        public CommandResult Execute(NaniPro.Managers.ScriptPlayerPro player, ref int ip)
        {
            bool v = player.EvalBool(cond);
            if (v) return default(CommandResult);
            // skip to next elseif/else/endif at the same depth
            bool dummy;
            ip = Flow.SkipToNext(ip, player.CurrentScript.Commands, out dummy);
            return new CommandResult { managedIndex = true };
        }
    }

    public class ElseIf : ICommand
    {
        public string cond;
        public ElseIf(string c) { cond = c; }
        public CommandResult Execute(NaniPro.Managers.ScriptPlayerPro player, ref int ip)
        {
            bool v = player.EvalBool(cond);
            if (v) return default(CommandResult);
            bool dummy;
            ip = Flow.SkipToNext(ip, player.CurrentScript.Commands, out dummy);
            return new CommandResult { managedIndex = true };
        }
    }

    public class Else : ICommand
    {
        public CommandResult Execute(NaniPro.Managers.ScriptPlayerPro player, ref int ip)
        {
            // Execute else body normally
            return default(CommandResult);
        }
    }

    public class EndIf : ICommand
    {
        public CommandResult Execute(NaniPro.Managers.ScriptPlayerPro player, ref int ip)
        {
            return default(CommandResult);
        }
    }

    public class Lang : ICommand
    {
        public string code;
        public Lang(string c) { code = c; }
        public CommandResult Execute(NaniPro.Managers.ScriptPlayerPro player, ref int ip)
        {
            if (player.vars != null) player.vars.Set("_lang", code);
            return default(CommandResult);
        }
    }

    // Helper flow utilities
    public static class Flow
    {
        public static int SkipToNext(int ip, List<ICommand> cmds, out bool matchedElseOrElseIf)
        {
            matchedElseOrElseIf = false;
            int i = ip + 1;
            int depth = 0;
            for (; i < cmds.Count; i++)
            {
                var c = cmds[i];
                if (c is If) depth++;
                else if (c is EndIf)
                {
                    if (depth == 0) return i; // position of endif; caller will set managedIndex so loop advances
                    depth--;
                }
                else if (depth == 0 && (c is ElseIf || c is Else))
                {
                    matchedElseOrElseIf = true;
                    return i;
                }
            }
            return i;
        }
    }
}
