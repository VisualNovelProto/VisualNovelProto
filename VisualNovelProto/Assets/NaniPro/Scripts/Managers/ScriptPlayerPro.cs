
using System.Collections;
using UnityEngine;

namespace NaniPro.Managers
{
    using NaniPro.Scripting;

    public class ScriptPlayerPro : MonoBehaviour
    {
        public BackgroundManagerPro backgrounds;
        public CharacterManagerPro characters;
        public TextPrinterManagerPro printer;
        public ChoiceHandlerManagerPro choices;
        public VariableStore vars;
        public SaveManagerPro saver;

        public ScriptPro CurrentScript { get; private set; }
        public int? DeferredJumpIndex { get; set; }

        public int ResolveLabel(string label) => CurrentScript != null ? CurrentScript.GetLabelIndex(label) : -1;

        public bool EvalBool(string expr)
        {
            // Very small evaluator: supports comparisons and && ||
            // Examples: a > 2, flag, name == "준우"
            expr = expr.Trim();
            // Handle || with short-circuit
            var orParts = expr.Split(new string[] {"||"}, System.StringSplitOptions.None);
            bool result = false;
            for (int i=0;i<orParts.Length;i++)
            {
                var andParts = orParts[i].Split(new string[] {"&&"}, System.StringSplitOptions.None);
                bool andRes = true;
                foreach (var p in andParts)
                {
                    bool atom = EvalAtom(p.Trim());
                    andRes = andRes && atom;
                    if (!andRes) break;
                }
                result = result || andRes;
                if (result) break;
            }
            return result;
        }

        bool EvalAtom(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            // equality / comparison
            string[] ops = new[] {">=", "<=", "==", "!=", ">", "<"};
            foreach (var op in ops)
            {
                int idx = s.IndexOf(op);
                if (idx > 0)
                {
                    var left = s.Substring(0, idx).Trim();
                    var right = s.Substring(idx + op.Length).Trim();
                    object lv = ResolveValue(left);
                    object rv = ResolveValue(right);
                    double ln, rn;
                    switch (op)
                    {
                        case "==": return string.Equals(lv?.ToString(), rv?.ToString());
                        case "!=": return !string.Equals(lv?.ToString(), rv?.ToString());
                        case ">=": if (double.TryParse(lv?.ToString(), out ln) && double.TryParse(rv?.ToString(), out rn)) return ln >= rn; break;
                        case "<=": if (double.TryParse(lv?.ToString(), out ln) && double.TryParse(rv?.ToString(), out rn)) return ln <= rn; break;
                        case ">":  if (double.TryParse(lv?.ToString(), out ln) && double.TryParse(rv?.ToString(), out rn)) return ln > rn; break;
                        case "<":  if (double.TryParse(lv?.ToString(), out ln) && double.TryParse(rv?.ToString(), out rn)) return ln < rn; break;
                    }
                    return false;
                }
            }
            // bare variable or literal
            var v = ResolveValue(s);
            if (v is bool b) return b;
            double n;
            if (double.TryParse(v?.ToString(), out n)) return n != 0;
            return !string.IsNullOrEmpty(v?.ToString());
        }

        object ResolveValue(string token)
        {
            token = token.Trim();
            if (token.StartsWith("\"") && token.EndsWith("\"")) return token.Substring(1, token.Length-2);
            if (bool.TryParse(token, out var b)) return b;
            if (double.TryParse(token, out var d)) return d;
            return vars.Get(token);
        }

        public IEnumerator Play(ScriptPro script, string startLabel)
        {
            CurrentScript = script;
            DeferredJumpIndex = null;
            if (script == null) yield break;

            int ip = script.GetLabelIndex(startLabel);
            if (ip < 0) { Debug.LogError("[NaniPro] Start label not found: " + startLabel); yield break; }

            while (ip < script.Commands.Count)
            {
                var cmd = script.Commands[ip];
                var res = cmd.Execute(this, ref ip);

                if (res.routine != null)
                    yield return StartCoroutine(res.routine);

                if (DeferredJumpIndex.HasValue)
                {
                    ip = DeferredJumpIndex.Value;
                    DeferredJumpIndex = null;
                }
                else if (!res.managedIndex)
                {
                    ip++;
                }
            }
        }

        // Save/Load
        public void Save(int slot, string scriptName, string label, int index)
        {
            var st = new SaveState{
                scriptName = scriptName,
                label = label,
                index = index,
                variables = vars.Snapshot(),
                backgroundPath = backgrounds.CurrentPath
            };
            var list = characters.Snapshot();
            foreach (var a in list)
                st.actors.Add(new CharacterSnapshot{ name = a.name, appearance = a.appearance, anchor = a.anchor });
            saver.Save(slot, st);
        }

        public IEnumerator Restore(SaveState st)
        {
            if (st == null) yield break;
            // Background
            if (!string.IsNullOrEmpty(st.backgroundPath))
                yield return backgrounds.SetBackground(st.backgroundPath, 0f);
            // Variables
            vars.Restore(st.variables);
            // Characters
            foreach (var a in st.actors)
            {
                var pos = AnchorPos.center;
                if (a.anchor == "left") pos = AnchorPos.left;
                else if (a.anchor == "right") pos = AnchorPos.right;
                yield return characters.Show(a.name, pos, a.appearance, 0f);
            }
        }
    }
}
