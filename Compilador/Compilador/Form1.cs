using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows.Forms;

namespace Compilador
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();

            var rm = new RuleManager();
            rm.Rules.Add(rm.CreateTextRule("E->TP"));
            rm.Rules.Add(rm.CreateTextRule("P->vTP/?"));
            rm.Rules.Add(rm.CreateTextRule("T->FO"));
            rm.Rules.Add(rm.CreateTextRule("O->&FO/?"));
            rm.Rules.Add(rm.CreateTextRule("F->~F/i"));

            rm.Rules.ForEach(x => lblProduction.Text += x.ToString() + Environment.NewLine);

            foreach (Rule rule in rm.Rules)
            {
                Console.Write($"{rule,-15}");
                Console.Write("{0, -15}", string.Join(",", rm.First(rule.Definitions)));
                Console.Write("{0, -15}", string.Join(",", rm.Follow(rule)));
                Console.WriteLine();
            }

            var listOfNames = rm.Rules.Select(x => x.Name).ToList();
            var terminais = new List<string>();

            foreach (var rule in rm.Rules)
            {
                var segments = rule.ToString().Split(new[] { "->" }, StringSplitOptions.None);
                var ruleText = segments[1];
                terminais.Add(ruleText);
            }

            var listOfChars = new List<string>();
            terminais.ForEach(prod => listOfChars.AddRange(prod.Select(c => c.ToString())));

            var naoTerminais = listOfChars
                .Where(x => !listOfNames.Contains(x.ToString()) && x != "?" && x != "/")
                .ToList();

            naoTerminais.Add("$");

            foreach (var naoTerminal in naoTerminais)
            {
                grid.Columns.Add(naoTerminal.ToString(), naoTerminal.ToString());
            }

            int i = 0;
            foreach (var item in listOfNames)
            {
                grid.Rows.Add();
                grid.Rows[i].HeaderCell.Value = item;
                i++;
            }

            foreach (var rule in rm.Rules)
            {
                var firsts = rm.First(rule.Definitions).Select(x => x.Name).ToList();
                var follows = rm.Follow(rule).Select(x => x.Name).ToList();

                foreach (DataGridViewRow row in grid.Rows)
                {
                    if (row.HeaderCell.Value?.ToString() == rule.Name)
                    {
                        if (rule.Empty != null && !string.IsNullOrEmpty(rule.Empty))
                        {
                            foreach (var follow in follows)
                            {
                                foreach (DataGridViewColumn col in grid.Columns)
                                {
                                    if (col.HeaderCell.Value?.ToString() == follow)
                                    {
                                        grid[col.Index, row.Index].Value = rule.Name + $"->{rule.Empty} (follow)";
                                    }
                                }
                            }
                        }

                        if (rule.NotEmpty != null && !string.IsNullOrEmpty(rule.NotEmpty))
                        {
                            foreach (var first in firsts)
                            {
                                foreach (DataGridViewColumn col in grid.Columns)
                                {
                                    if (col.HeaderCell.Value?.ToString() == first)
                                    {
                                        grid[col.Index, row.Index].Value = rule.Name + $"->{rule.NotEmpty} (first)";
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    public interface IRule
    {
        string Name { get; }
    }

    public class Token : IRule
    {
        public string Name { get; }

        public Token(string name)
        {
            Name = name;
        }

        public override string ToString()
        {
            return Name;
        }

        public static Token Eof = new Token("$");
        public static Token Empty = new Token("?");
    }

    public class Rule : IRule
    {
        public string Name { get; }

        public IEnumerable<IEnumerable<IRule>> Definitions { get; }

        private string _empty { get; set; }
        private string _notEmpty { get; set; }
        public string Empty { get => GetEmpty(); }
        public string NotEmpty { get => GetNotEmpty(); }

        public Rule(string name, IEnumerable<IEnumerable<IRule>> definitions)
        {
            Name = name;
            Definitions = definitions;
        }

        public override string ToString()
        {
            try
            {
                return $"{Name}->" + string.Join("/", Definitions.Select(definition => string.Join("", definition.Select(rule => rule.Name))));
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return string.Empty;
            }
        }

        private string GetEmpty()
        {
            if(_empty == null)
            {
                var segments = ToString()
                    .Split(new[] { "->", "/" }, StringSplitOptions.None)
                    .Where(x => x != Name);
                _empty = segments.Where(x => x.Contains("?")).Select(x => x).FirstOrDefault()?.ToString();
            }
            
            return _empty;
        }

        private string GetNotEmpty()
        {
            if(_notEmpty == null)
            {
                var segments = ToString()
                    .Split(new[] { "->", "/" }, StringSplitOptions.None)
                    .Where(x => x != Name);
                _notEmpty = segments.Where(x => !x.Contains("?")).Select(x => x).FirstOrDefault()?.ToString();
            }
            
            return _notEmpty;
        }
    }

    public class RuleManager
    {
        public List<Rule> Rules { get; } = new List<Rule>();

        private Dictionary<string, Token> TokenMap = new Dictionary<string, Token>();

        private Token GetOrCreateToken(string tokenName)
        {
            if (tokenName == "$")
                return Token.Eof;
            if (tokenName == "?")
                return Token.Empty;
            if (!TokenMap.ContainsKey(tokenName))
                TokenMap.Add(tokenName, new Token(tokenName));
            return TokenMap[tokenName];
        }

        public Rule CreateTextRule(string text)
        {
            var segments = text.Split(new[] { "->" }, StringSplitOptions.None);
            var name = segments[0];
            var ruleText = segments[1];

            var ruleDefinitions = ruleText
                .Split('/')
                .Select(ruleSequenceText =>
                {
                    return ruleSequenceText.Select(token =>
                    {
                        return char.IsUpper(token) ?
                            Rules.First(x => x.Name == token.ToString()) :
                            (IRule)GetOrCreateToken(token.ToString());
                    });
                });
            return new Rule(name, ruleDefinitions);
        }

        public IEnumerable<Token> First(IEnumerable<IEnumerable<IRule>> definitions)
        {
            return definitions.SelectMany(ruleSequence =>
            {
                var tokens = new List<Token>();
                var hasEmpty = true;
                foreach (var item in ruleSequence)
                {
                    if (item is Token token)
                    {
                        if (token != Token.Empty)
                        {
                            hasEmpty = false;
                            tokens.Add(token);
                            break;
                        }
                    }
                    else if (item is Rule rule)
                    {
                        var ruleFirsts = First(rule.Definitions);
                        if (!ruleFirsts.Contains(Token.Empty))
                        {
                            hasEmpty = false;
                            tokens.AddRange(ruleFirsts);
                            break;
                        }
                        else
                        {
                            tokens.AddRange(ruleFirsts.Where(x => x != Token.Empty));
                        }
                    }
                }
                if (hasEmpty) tokens.Add(Token.Empty);
                return tokens;
            })
            .Distinct();
        }

        public IEnumerable<Token> Follow(Rule theRule)
        {
            return Rules
                .SelectMany(knownRule =>
                {
                    return knownRule.Definitions
                        .Where(ruleSequence => ruleSequence.Contains(theRule))
                        .SelectMany(relatedSequence =>
                        {
                            var tokens = new List<Token>();
                            do
                            {
                                relatedSequence = relatedSequence
                                    .SkipWhile(x => x != theRule)
                                    .Skip(1);

                                if (relatedSequence.Any())
                                {
                                    var firsts = First(new[] { relatedSequence });
                                    tokens.AddRange(firsts.Where(x => x != Token.Empty));
                                    if (firsts.Contains(Token.Empty))
                                    {
                                        tokens.AddRange(Follow(knownRule));
                                    }
                                }
                                else if (knownRule != theRule)
                                {
                                    tokens.AddRange(Follow(knownRule));
                                }
                            } while (relatedSequence.Contains(theRule));
                            return tokens;
                        });
                })
                .DefaultIfEmpty(Token.Eof)
                .Distinct();
        }

        public void DumpFF()
        {

        }
    }
}
