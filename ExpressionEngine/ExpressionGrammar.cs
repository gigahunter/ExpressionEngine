﻿using ExpressionEngine.Rules;
using Sprache;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ExpressionEngine
{
    public class ExpressionGrammar
    {
        private readonly IList<IFunctionDefinition> _functionDefinitions;
        private readonly Parser<IRule> _method;
        private readonly Parser<Task<ValueContainer>> _input;

        public ExpressionGrammar(IEnumerable<FunctionMetadata> functions, IEnumerable<IFunctionDefinition> functionDefinitions, IServiceProvider serviceProvider)
        {
            _functionDefinitions = functionDefinitions?.ToList();

            var functionCollection = functions.ToList() ?? throw new ArgumentNullException(nameof(functions));

            #region BasicAuxParsers

            Parser<IRule> boolean = Parse.String("true").Select(b => new ConstantRule(new ValueContainer(true)))
                .Or(Parse.String("false").Select(b => new ConstantRule(new ValueContainer(false))));

            Parser<IRule> integer =
                Parse.Digit.AtLeastOnce().Text()
                    .Select(
                        constString => new ConstantRule(new ValueContainer(constString, true))
                    );

            Parser<IRule> number = // decimalInvariant.Or(integer);
                from sign in Parse.Char('-').Or(Parse.Char('+')).Optional()
                from number1 in Parse.DecimalInvariant.Or(Parse.Digit.AtLeastOnce().Text())
                select sign.IsDefined && sign.Get().Equals('-')
                    ? new ConstantRule(new ValueContainer('-' + number1, true))
                    : new ConstantRule(new ValueContainer(number1, true));

            Parser<string> simpleString =
                Parse.AnyChar.Except(Parse.Char('@')).AtLeastOnce().Text();

            Parser<char> escapedCharacters =
                from c in
                    Parse.String("''").Select(n => '\'')
                        .Or(Parse.String("''").Select(n => '\''))
                select c;

            Parser<IRule> stringLiteral =
                from content in Parse.CharExcept('\'').Or(escapedCharacters).Many().Text()
                    .Contained(Parse.Char('\''), Parse.Char('\''))
                select new StringLiteralRule(new ValueContainer(content));

            Parser<string> allowedCharacters =
                Parse.String("@@").Select(_ => '@')
                    .Or(Parse.AnyChar)
                    .Except(Parse.String("@{"))
                    .Select(c => c.ToString());

            #endregion BasicAuxParsers

            var lBracket = Parse.Char('[');
            var rBracket = Parse.Char(']');
            var lParenthesis = Parse.Char('(');
            var rParenthesis = Parse.Char(')');

            Parser<bool> nullConditional = Parse.Char('?').Optional().Select(nC => !nC.IsEmpty);

            Parser<IRule> bracketIndices =
                from nll in nullConditional
                from index in _method.Or(stringLiteral).Or(integer).Contained(lBracket, rBracket)
                select new IndexRule(index, nll);

            Parser<IRule> dotIndices =
                from nll in nullConditional
                from dot in Parse.Char('.')
                from index in Parse.AnyChar.Except(
                    Parse.Chars('[', ']', '{', '}', '(', ')', '@', ',', '.', '?')
                ).Many().Text()
                select new IndexRule(new StringLiteralRule(new ValueContainer(index)), nll);

            Parser<IRule> argument =
                from arg in Parse.Ref(() => _method.Or(stringLiteral).Or(number).Or(boolean))
                select arg;

            Parser<IOption<IEnumerable<IRule>>> arguments =
                from args in argument.Token().DelimitedBy(Parse.Char(',')).Optional()
                select args;

            Parser<IRule> function =
                from mandatoryLetter in Parse.Letter
                from rest in Parse.LetterOrDigit.Many().Text()
                from args in arguments.Contained(lParenthesis, rParenthesis)
                select new ExpressionRule(functionCollection, serviceProvider, mandatoryLetter + rest,
                    args.IsEmpty
                        ? null
                        : args.Get());

            _method =
                Parse.Ref(() =>
                    from func in function
                    from indexes in bracketIndices.Or(dotIndices).Many()
                    select indexes.Aggregate(func, (acc, next) => new AccessValueRule(acc, next)));

            Parser<ValueTask<ValueContainer>> enclosedExpression =
                _method.Contained(
                        Parse.String("@{"),
                        Parse.Char('}'))
                    .Select(x => x.Evaluate());

            Parser<Task<ValueContainer>> expression =
                Parse.Char('@').SelectMany(at => _method, async (at, method) => await method.Evaluate());

            Parser<string> allowedString =
                from t in simpleString.Or(allowedCharacters).Many()
                select string.Concat(t);

            Parser<Task<ValueContainer>> joinedString = allowedString
                .SelectMany(preFix => enclosedExpression.Optional(),
                    async (preFix, exp) => exp.IsEmpty ? preFix : preFix + await exp.Get())
                .Many()
                .Select(async e => await Task.FromResult(new ValueContainer(string.Concat(await Task.WhenAll(e)))));

            _input = expression.Or(joinedString);
        }

        public async ValueTask<string> EvaluateToString(string input)
        {
            var output = await PreAnalyzeAndParse(input);

            return output.GetValue<string>();
        }

        public async ValueTask<ValueContainer> EvaluateToValueContainer(string input)
        {
            return await PreAnalyzeAndParse(input);
        }

        private async ValueTask<ValueContainer> PreAnalyzeAndParse(string input)
        {
            if (_functionDefinitions != null && _functionDefinitions.Count > 0)
            {
                input = _functionDefinitions.Aggregate(input,
                    (current, functionDefinition) =>
                        current.Replace(functionDefinition.From, functionDefinition.To));
            }

            return await _input.Parse(input);
        }
    }
}