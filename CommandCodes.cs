using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SimpleParserUtil
{
    public class CommandCodes
    {
        private List<string> Commands { get; }
        private List<int> InstanceID { get; }
        public delegate bool Action(ref Transform TS);
        public Dictionary<string, Action> Terminals { get; private set; }
        public List<(string, string)> Nonterminals { get; private set; }
        public string cmd { get; private set; } = "";
        public CommandCodes(IEnumerable<int> InstanceID, IEnumerable<string> Commands)
        {
            this.InstanceID = InstanceID.ToList();
            this.Commands = Commands.ToList();
            Configure();
        }
        public static string CleanString(string Str)
        {
            while (Str[0] == ' ')
            {
                Str = Str.Remove(0, 1);
            }
            while (Str.Last() == ' ')
            {
                Str = Str.Remove(Str.Length - 1);
            }
            return Str;
        }
        private bool GetTerminalToken(List<InstanceDefinition> Defs)
        {
            this.Terminals = new Dictionary<string, Action>();
            var AddTS = new List<string>();
            foreach (var Def in Defs)
            {
                var Tokens = Def.GetUserString("TransformToken").Split(',').ToList();
                if (Tokens.Count != 0)
                {
                    foreach (var Token in Tokens)
                    {
                        var TSstring = Def.GetUserString(Token);

                        if (TSstring == null) continue; // Null check for TSstring

                        if (Terminals.ContainsKey(Token) && !AddTS.Contains(TSstring))
                        {
                            cmd += "Duplicate token with different TSstring";
                            return false; // Duplicate token with different TSstring
                        }
                        else if (!Terminals.ContainsKey(Token))
                        {
                            try
                            {
                                var TS = JsonSerializer.Deserialize<Transform>(TSstring);

                                this.Terminals.Add(
                                    Token,
                                    (ref Transform x) =>
                                    {
                                        x = x * TS;
                                        return true;
                                    }
                                );
                                AddTS.Add(TSstring);
                            }
                            catch (JsonException ex)
                            {
                                cmd += $"Failed to deserialize Transform for token '{Token}': {ex.Message}";
                                return false; // Handle deserialization error
                            }
                        }
                    }
                }
                var NameToken = Def.GetUserString("Name");
                if (NameToken != null && !Terminals.ContainsKey(NameToken))
                {
                    this.Terminals.Add(
                        NameToken,
                        (ref Transform x) =>
                        {
                            var ID = RhinoDoc.ActiveDoc.InstanceDefinitions.Find(NameToken);
                            if (ID != null)
                            {
                                RhinoDoc.ActiveDoc.Objects.AddInstanceObject(ID.Index, x);
                                return true;
                            }
                            return false; // Return false if the ID is not found
                        }
                    );
                }
            }
            return true;
        }
        private bool GetNonterminalToken()
        {
            Nonterminals = new List<(string, string)>();
            for (int i = 0; i < this.Commands.Count; i++)
            {
                string Code = this.Commands[i];
                if (Code.Contains("//") || Code.Contains("#")) continue;
                if (Code.Contains("="))
                {
                    string Head = CleanString(Code.Split('=')[0]);
                    if (Code.Split('=')[1].Contains("="))
                    {
                        cmd += "two or more = ";
                        return false;
                    }
                    if (Code.Contains("|"))
                    {
                        string[] Bodies = Code.Split('=')[1].Split('|');
                        foreach (var body in Bodies)
                        {
                            this.Nonterminals.Add((Head, CleanString(body)));
                        }
                    }
                    else
                    {
                        this.Nonterminals.Add((Head, CleanString(Code.Split('=')[1])));
                    }
                }
            }
            if (Nonterminals.Count == 0)
            {
                cmd += "No nonterminals are set";
                return false;
            }
            return true;
        }
        private bool RunDefined()
        {
            foreach (var Code in Commands)
            {
                if (Code.Contains("#Defined"))
                {
                    var SplitsElement = Code.Split(' ');
                    if (SplitsElement.Length != 3)
                    {
                        cmd += Code + " has error";
                        return false;
                    }
                    var NewNonterminals = new List<(string, string)>();
                    for (int i = 0; i < this.Nonterminals.Count; i++)
                    {
                        var Content = this.Nonterminals[i].Item2;
                        if (Content.Contains(SplitsElement[1])) ;
                        Content = Content.Replace(SplitsElement[1], SplitsElement[2]);
                        NewNonterminals.Add((this.Nonterminals[i].Item1, Content));
                    }
                    this.Nonterminals = NewNonterminals;
                }
            }
            return true;
        }
        private bool ReConfigureNonterminalContent()
        {
            var TempList = new List<(string, string)>();
            foreach (var Code in this.Nonterminals)
            {
                var Content = Code.Item2;
                var Tokens = Content.Split(' ');
                var NewList = new List<string>();
                for (int k = 0; k < Tokens.Length; k++)
                {
                    var Token = Tokens[k];
                    if (int.TryParse(Token, out var N))
                    {
                        if (N > 0)
                        {
                            k++;
                            Token = Tokens[k];
                            for (int i = 0; i < N; i++)
                                NewList.Add(Token);
                        }
                    }
                    else
                        NewList.Add(Token);
                }
                Content = string.Join(" ", NewList);
                TempList.Add((Code.Item1, Content));
            }
            this.Nonterminals = TempList;
            return true;
        }
        private bool Configure()
        {
            var Doc = Rhino.RhinoDoc.ActiveDoc;
            var InstanceObjects = new List<InstanceDefinition>();
            for (int i = 0; i < Doc.InstanceDefinitions.Count; i++)
            {
                if (InstanceID.Contains(i))
                    InstanceObjects.Add(Doc.InstanceDefinitions[i]);
            }
            if (!GetTerminalToken(InstanceObjects))
            {
                cmd += "Terminal failed";
                return false;
            }
            if (!GetNonterminalToken())
            {
                cmd += "Nonterminal failed";
                return false;
            }
            if (!RunDefined())
            {
                cmd += "Defined Error";
                return false;
            }
            ReConfigureNonterminalContent();

            cmd += "Configure finished\n==================================\n";
            return true;
        }
        private string RandomPick(IEnumerable<string> Strs)
        {
            var Rand = new Random(System.DateTime.Today.Millisecond);
            return Strs.ToList()[Rand.Next(Strs.ToList().Count) - 1];
        }
        private string Space = " ";
        private bool Recurvsive(string Axiom, ref Transform Position)
        {
            var Contents = this.Nonterminals.Where(x => x.Item1 == Axiom).Select(x => x.Item2).ToList();
            if (Contents.Count == 0)
            {
                cmd += $"Cannot find {Axiom}";
                return false;
            }
            var CurrentContent = Contents.Count > 1 ? RandomPick(Contents) : Contents[0];

            var AxiomTokens = CurrentContent.Split(' ');
            var NonterminalsHeads = this.Nonterminals.Select(x => x.Item1).ToList();
            cmd += Space + $"{Axiom} : \n";
            foreach (var Token in AxiomTokens)
            {
                if (NonterminalsHeads.Contains(Token))
                {
                    Space += " ";
                    Recurvsive(Token, ref Position);
                    Space = Space.Remove(Space.Length - 1);
                }
                else if (this.Terminals.ContainsKey(Token))
                {
                    Space += "   ";
                    cmd += Space + $"{Token}\n";
                    var ActionResult = Terminals[Token](ref Position);
                    Space = Space.Remove(Space.Length - 3);
                }

            }
            return true;
        }
        public bool Compute(string Axiom, Transform StartPosition = new Transform())
        {
            Transform InitialTransform = Transform.Identity;
            if (!(StartPosition == new Transform()))
                InitialTransform = StartPosition;

            var Contents = this.Nonterminals.Where(x => x.Item1 == Axiom).Select(x => x.Item2).ToList();
            if (Contents.Count == 0)
            {
                cmd += $"Cannot find {Axiom}";
                return false;
            }
            var CurrentContent = Contents.Count > 1 ? RandomPick(Contents) : Contents[0];

            var AxiomTokens = CurrentContent.Split(' ');
            var NonterminalsHeads = this.Nonterminals.Select(x => x.Item1).ToList();
            cmd += Space + $"{Axiom} : \n";
            foreach (var Token in AxiomTokens)
            {
                if (NonterminalsHeads.Contains(Token))
                {
                    Space += " ";
                    Recurvsive(Token, ref InitialTransform);
                    Space = Space.Remove(Space.Length - 1);
                }
                else if (this.Terminals.ContainsKey(Token))
                {
                    Space += "   ";
                    cmd += Space + $"{Token}\n";
                    var ActionResult = Terminals[Token](ref InitialTransform);
                    Space = Space.Remove(Space.Length - 3);
                }
            }
            return true;
        }
        private bool RecurvsiveStepbyStep(string Axiom, ref List<(string, Action)> Actions)
        {
            var Contents = this.Nonterminals.Where(x => x.Item1 == Axiom).Select(x => x.Item2).ToList();
            if (Contents.Count == 0)
            {
                cmd += $"Cannot find {Axiom}";
                return false;
            }
            var CurrentContent = Contents.Count > 1 ? RandomPick(Contents) : Contents[0];

            var AxiomTokens = CurrentContent.Split(' ');
            var NonterminalsHeads = this.Nonterminals.Select(x => x.Item1).ToList();
            cmd += Space + $"{Axiom} : \n";
            foreach (var Token in AxiomTokens)
            {
                if (NonterminalsHeads.Contains(Token))
                {
                    Space += " ";
                    RecurvsiveStepbyStep(Token, ref Actions);
                    Space = Space.Remove(Space.Length - 1);
                }
                else if (this.Terminals.ContainsKey(Token))
                {
                    Space += "   ";
                    cmd += Space + $"{Token}\n";
                    Actions.Add((Token, Terminals[Token]));
                    Space = Space.Remove(Space.Length - 3);
                }

            }
            return true;
        }

        public bool ComputeStepbyStep(string Axiom, ref List<(string, Action)> Actions)
        {
            Actions = new List<(string, Action)>();
            var Contents = this.Nonterminals.Where(x => x.Item1 == Axiom).Select(x => x.Item2).ToList();
            if (Contents.Count == 0)
            {
                cmd += $"Cannot find {Axiom}";
                return false;
            }
            var CurrentContent = Contents.Count > 1 ? RandomPick(Contents) : Contents[0];

            var AxiomTokens = CurrentContent.Split(' ');
            var NonterminalsHeads = this.Nonterminals.Select(x => x.Item1).ToList();
            cmd += Space + $"{Axiom} : \n";
            foreach (var Token in AxiomTokens)
            {
                if (NonterminalsHeads.Contains(Token))
                {
                    Space += " ";
                    RecurvsiveStepbyStep(Token, ref Actions);
                    Space = Space.Remove(Space.Length - 1);
                }
                else if (this.Terminals.ContainsKey(Token))
                {
                    Space += "   ";
                    cmd += Space + $"{Token}\n";
                    Actions.Add((Token, Terminals[Token]));
                    Space = Space.Remove(Space.Length - 3);
                }
            }
            return true;
        }
        public bool BakeStepByStep(List<(string, Action)> Actions, ref int Iteration, ref Transform TS)
        {
            if (Iteration >= Actions.Count)
                return true; //Done

            var CallAction = Actions[Iteration];
            var Result = CallAction.Item2(ref TS);
            Iteration++;
            return !Result;
        }
    }
}
