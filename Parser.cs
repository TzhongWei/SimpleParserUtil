using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleParserUtil
{
    // Grammar Rule for parser
    /*
    <Start> = #SS
    <DOWN> = L R | L <DOWNH> R
    <DOWNH> = S <DOWNH> | empty
    <UP> = R L | R <UPN> L
    <UPN> = S <UPN> | empty
    <ADD> = <DOWN> S | <UP> S
    <R> = SS#
    */
    public enum TokenType { Hash, S, R, L, EOF }

    public class Token
    {
        public TokenType Type { get; }
        public Token(TokenType type) { Type = type; }
    }
    public class Parser
    {
        private List<Token> Tokens;
        private int position = 0;
        private StringBuilder output = new StringBuilder();
        private StringBuilder ErrorMessage = new StringBuilder();

        public static void RunParser(string Sentence, ref string Semantic)
        {
            Parser parser = new Parser(Sentence);
            if (parser.Parse())
                //Semantic = parser.GetOutput();
                Semantic = parser.SortLanguage();
            else
                Semantic = parser.GetOutput() + ": Failed";
        }

        public Parser(string input)
        {
            Tokens = Tokenise(input);
        }

        private List<Token> Tokenise(string input)
        {
            var tokens = new List<Token>();
            foreach (var c in input)
            {
                switch (c)
                {
                    case '#': tokens.Add(new Token(TokenType.Hash)); break;
                    case 'S': tokens.Add(new Token(TokenType.S)); break;
                    case 'R': tokens.Add(new Token(TokenType.R)); break;
                    case 'L': tokens.Add(new Token(TokenType.L)); break;
                }
            }
            tokens.Add(new Token(TokenType.EOF));
            return tokens;
        }

        private Token CurrentToken => Tokens[position];
        private void Advance() => position++;
        private bool Match(TokenType type)
        {
            if (CurrentToken.Type == type)
            {
                Advance();
                return true;
            }
            else
                return false;
        }

        public bool Parse()
        {
            bool result = ParseAxiom() && Match(TokenType.EOF);
            if (!result)
                output.Append(this.ErrorMessage.ToString());
            return result;
        }

        public bool ParseAxiom()
        {
            if (ParseStart())
            {
                if (ParseMid())
                {
                    // <END> is epsilon; no action needed
                    output.Append("h h h a"); // From <END>
                    ErrorMessage.Clear();
                    return true;
                }
            }

            ErrorMessage.Append(" #Axiom ");
            return false;

        }

        private bool ParseStart()
        {
            if (Match(TokenType.Hash) && Match(TokenType.S) && Match(TokenType.S))
            {
                output.Append("a h h h ");
                ErrorMessage.Clear();
                return true;
            }
            ErrorMessage.Append(" #Start ");
            return false;
        }

        private bool ParseMid()
        {
            int savedPosition = position;

            if (ParseUP())
            {
                ErrorMessage.Clear();
                return true;
            }
            position = savedPosition;
            if (ParseDOWN())
            {
                ErrorMessage.Clear();
                return true;
            }
            position = savedPosition;
            if (ParseS())
            {
                ErrorMessage.Clear();
                return true;
            }
            position = savedPosition;
            if (ParseR())
            {
                ErrorMessage.Clear();
                return true;
            }
            ErrorMessage.Append(" #MID ");
            return false;
        }

        private bool ParseUP()
        {
            if (Match(TokenType.R))
            {
                output.Append("h a h ");
                if (ParseUPH())
                {
                    ErrorMessage.Clear();
                    output.Append("a ");
                    return true;
                }
            }
            ErrorMessage.Append(" #UP ");
            return false;
        }

        private bool ParseUPH()
        {
            var savePosition = position;
            if (Match(TokenType.S))
            {
                output.Append("h h ");
                if (ParseUPH())
                {
                    output.Append("h h ");
                    return true;
                }
            }
            position = savePosition;
            if (Match(TokenType.L))
            {
                output.Append("a ");
                if (ParseADD())
                {
                    output.Append("h a h ");
                    ErrorMessage.Clear();
                    return true;
                }
            }

            ErrorMessage.Append(" #UPH ");
            return false;
        }

        private bool ParseDOWN()
        {
            if (Match(TokenType.L))
            {
                output.Append("a ");
                if (ParseDOWNH())
                {
                    output.Append("h a h ");
                    ErrorMessage.Clear();
                    return true;
                }
            }
            ErrorMessage.Append(" #DOWN ");
            return false;
        }

        private bool ParseDOWNH()
        {
            var savePosition = position;
            if (Match(TokenType.S))
            {
                output.Append("h h ");
                if (ParseDOWNH())
                {
                    output.Append("h h ");
                    return true;
                }
            }
            position = savePosition;

            if (Match(TokenType.R))
            {
                output.Append("h a h ");
                if (ParseADD())
                {
                    output.Append("a ");
                    ErrorMessage.Clear();
                    return true;
                }
            }
            ErrorMessage.Append(" #DOWNH ");
            return false;
        }

        private bool ParseS()
        {
            int savePosition = position;
            if (Match(TokenType.S))
            {
                output.Append("h h ");

                if (ParseADD())
                {
                    output.Append("h h ");
                    ErrorMessage.Clear();
                    return true;
                }
            }
            position = savePosition;
            if (Match(TokenType.S) && Match(TokenType.S))
            {
                output.Append("h h h h ");
                if (ParseS())
                {
                    output.Append("h h h h ");
                    ErrorMessage.Clear();
                    return true;
                }
            }

            ErrorMessage.Append(" #S ");
            return false;
        }

        private bool ParseR()
        {
            if (Match(TokenType.S) && Match(TokenType.S) && Match(TokenType.Hash))
            {
                output.Append("h h h a a h h h ");
                ErrorMessage.Clear();
                return true;
            }
            ErrorMessage.Append(" #R ");
            return false;
        }

        private bool ParseADD()
        {
            int savedPosition = position;

            if (ParseR())
            {
                ErrorMessage.Clear();
                return true;
            }
            position = savedPosition;
            if (Match(TokenType.S))
            {
                output.Append("h h ");
                if (ParseMid())
                {
                    output.Append("h h ");
                    ErrorMessage.Clear();
                    return true;
                }
            }
            ErrorMessage.Append(" #ADD ");
            return false;
        }

        private bool ParseEnd()
        {
            // <END> is epsilon; no action needed
            ErrorMessage.Clear();
            return true;
        }
        public string SortLanguage()
        {
            string[] outStr = output.ToString().Split(' ');
            string FinalString = "";
            int DupCount = 1;
            for (int i = 0; i < outStr.Length - 1; i++)
            {
                if (outStr[i] == outStr[i + 1])
                    DupCount++;
                else
                {
                    if (DupCount == 1)
                        FinalString += outStr[i] + " ";
                    else
                    {
                        FinalString += DupCount.ToString() + " " + outStr[i] + " ";
                        DupCount = 1;
                    }
                }
            }
            FinalString += outStr.Last();
            return FinalString;
        }
        public string GetOutput() => output.ToString().Trim();
    }
}
