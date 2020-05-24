using System;
using System.Linq;
using System.Reflection;
using NCalc.Domain;
using L = System.Linq.Expressions;
using System.Collections.Generic;

namespace NCalc
{
    internal enum SRPOptions
    {
        none = 0,
        TrimItems = 1,
        RemoveEmptyEntries = 2,
        EmptyOnError = 4,
        RemoveGlobalParanthesis = 8,
        TrimNRem = RemoveGlobalParanthesis | RemoveEmptyEntries | TrimItems,
        All = 15
    }
    internal static class helperClass
    {
        internal static List<string> SplitRegardingPrenthesis(this string str, SRPOptions srpOptions = SRPOptions.TrimNRem, string delims = ",", bool splitOnParEdge = false, string Opars = "({[", string Cpars = ")}]", string quote = "<>", string escape = "\\")
        {
            return SplitRegardingPrenthesis(str, -1, srpOptions, delims, splitOnParEdge, Opars, Cpars, quote, escape);
        }
        internal static List<string> SplitRegardingPrenthesis(this string str, int maxItems, SRPOptions srpOptions = SRPOptions.TrimNRem, string delims = ",", bool splitOnParEdge = false, string Opars = "({[", string Cpars = ")}]", string quote = "<>", string escape = "\\")
        {
            return SplitRegardingPrenthesis(str, 0, -1, srpOptions, delims, splitOnParEdge, Opars, Cpars, quote, escape);
        }
        internal static List<string> SplitRegardingPrenthesis(this string str, int delimSkipped, int maxitems, SRPOptions srpOptions = SRPOptions.TrimNRem, string delims = ",", bool splitOnParEdge = false, string Opars = "({[", string Cpars = ")}]", string quote = "<>", string escape = "\\")
        {
            List<string> ret = new List<string>();
            IEnumerable<string> retTemp = ret;
            Stack<char> parStack = new Stack<char>();
            string curStr = "";
            bool err = false;
            int pos = -1;
            bool openItem = false;
            bool inQuote = false;
            bool escaped = false;
            foreach (char c in str)
            {
                pos++;
                if (err)
                {
                    break;
                }
                int qi = quote.IndexOf(c);
                if (inQuote)
                {
                    openItem = true;
                    curStr += c;
                    int ei = escape.IndexOf(c);
                    inQuote = escaped ? inQuote : qi < 0; //toggle inQuote to false if we encountered an unescaped quote char
                    escaped = escaped ? false : ei >= 0; //toggle escape to false if we encountered an unescaped escape char
                    continue;
                }
                int di = delims.IndexOf(c);
                if (di >= 0 && delimSkipped > 0)
                {
                    delimSkipped--;
                    di = -1;
                }
                if (!parStack.Any() && di >= 0)
                {
                    ret.Add(curStr);
                    curStr = "";
                    openItem = true;
                    if (maxitems > 0 && curStr.Length == 0)
                    {
                        if (ret.Count + 1 >= maxitems)
                        {
                            ret.Add(str.Substring(pos + 1)); //+1 to skip the delim itself
                            retTemp = ret;
                            if (srpOptions.HasFlag(SRPOptions.TrimItems))
                            {
                                retTemp = retTemp.Select(s => s.Trim());
                            }
                            if (srpOptions.HasFlag(SRPOptions.RemoveEmptyEntries))
                            {
                                retTemp = retTemp.Where(s => s.Any());
                            }
                            return retTemp.ToList();
                        }
                    }
                    continue;
                }

                int opi = Opars.IndexOf(c);
                int cpi = Cpars.IndexOf(c);
                if (opi >= 0)
                {
                    if (splitOnParEdge && !parStack.Any() && curStr.Any())
                    {
                        ret.Add(curStr);
                        curStr = "";
                    }
                    parStack.Push(Cpars[opi]);
                    curStr += c;
                    openItem = true;
                }
                else if (cpi >= 0)
                {
                    if (parStack.Any() && parStack.First() == c)
                    {
                        parStack.Pop(); //ToDo: is this lifo? we are assuming it is
                    }
                    else
                    {
                        err = true;
                        break;
                    }
                    openItem = true;
                    curStr += c;
                    if (splitOnParEdge && !parStack.Any() && curStr.Any())
                    {
                        ret.Add(curStr);
                        curStr = "";
                        openItem = false;
                        continue;
                    }
                }
                else
                {
                    openItem = true;
                    curStr += c;
                }
            }
            if (openItem)
            {
                ret.Add(curStr);
            }
            if (err)
            {
                if (srpOptions.HasFlag(SRPOptions.EmptyOnError))
                {
                    return new List<string>();
                }
                else
                {
                    retTemp = str.Split(delims.ToCharArray());
                }
            }
            else
            {
                retTemp = ret;
            }
            if (srpOptions.HasFlag(SRPOptions.TrimItems))
            {
                retTemp = retTemp.Select(s => s.Trim());
            }
            if (srpOptions.HasFlag(SRPOptions.RemoveGlobalParanthesis) && retTemp.Count() == 1)
            {
                string retTempItem = retTemp.First();
                int opi = Opars.IndexOf(retTempItem[0]);
                int cpi = Cpars.IndexOf(retTempItem[retTempItem.Length - 1]);
                if (retTempItem.Length >= 2 && opi == cpi && opi >= 0)
                {
                    retTempItem = retTempItem.Substring(1, retTempItem.Length - 2);
                    return retTempItem.SplitRegardingPrenthesis(maxitems, srpOptions, delims, splitOnParEdge, Opars, Cpars, quote, escape); //Run again without the globally enclosing parantheses and return result
                    //ToDo:!! use insted IsEnclosedBy() in the beginning to make this more efficient
                }
            }
            if (srpOptions.HasFlag(SRPOptions.RemoveEmptyEntries))
            {
                retTemp = retTemp.Where(s => s.Any());
            }
            return retTemp.ToList();
        }

    }
        internal class LambdaExpressionVistor : LogicalExpressionVisitor
    {
        private readonly IDictionary<string, object> _parameters;
        private L.Expression _result;
        private readonly L.Expression _context;
        private readonly EvaluateOptions _options = EvaluateOptions.None;

        private bool AllowDerefMembers { get { return (_options & EvaluateOptions.AllowLambdaObjectMemberAccess) == EvaluateOptions.AllowLambdaObjectMemberAccess; } }
        private bool AllowInvokeFunctions { get { return (_options & EvaluateOptions.AllowLambdaObjectFunctionAccess) == EvaluateOptions.AllowLambdaObjectFunctionAccess; } }
        private bool AllowConversion { get { return (_options & EvaluateOptions.AllowLambdaParameterConversion) == EvaluateOptions.AllowLambdaParameterConversion; } }
        private bool Ordinal { get { return (_options & EvaluateOptions.MatchStringsOrdinal) == EvaluateOptions.MatchStringsOrdinal; } }
        private bool IgnoreCaseString { get { return (_options & EvaluateOptions.MatchStringsWithIgnoreCase) == EvaluateOptions.MatchStringsWithIgnoreCase; } }
        private bool Checked { get { return (_options & EvaluateOptions.OverflowProtection) == EvaluateOptions.OverflowProtection; } }

        public LambdaExpressionVistor(IDictionary<string, object> parameters, EvaluateOptions options)
        {
            _parameters = parameters;
            _options = options;
        }

        public LambdaExpressionVistor(L.ParameterExpression context, EvaluateOptions options)
        {
            _context = context;
            _options = options;
        }

        public L.Expression Result => _result;

        public override void Visit(LogicalExpression expression)
        {
            throw new NotImplementedException();
        }

        public override void Visit(TernaryExpression expression)
        {
            expression.LeftExpression.Accept(this);
            var test = _result;

            expression.MiddleExpression.Accept(this);
            var ifTrue = _result;

            expression.RightExpression.Accept(this);
            var ifFalse = _result;

            _result = L.Expression.Condition(test, ifTrue, ifFalse);
        }

        public override void Visit(BinaryExpression expression)
        {
            expression.LeftExpression.Accept(this);
            var left = _result;

            expression.RightExpression.Accept(this);
            var right = _result;

            switch (expression.Type)
            {
                case BinaryExpressionType.And:
                    _result = L.Expression.AndAlso(left, right);
                    break;
                case BinaryExpressionType.Or:
                    _result = L.Expression.OrElse(left, right);
                    break;
                case BinaryExpressionType.NotEqual:
                    _result = WithCommonNumericType(left, right, L.Expression.NotEqual, expression.Type);
                    break;
                case BinaryExpressionType.LesserOrEqual:
                    _result = WithCommonNumericType(left, right, L.Expression.LessThanOrEqual, expression.Type);
                    break;
                case BinaryExpressionType.GreaterOrEqual:
                    _result = WithCommonNumericType(left, right, L.Expression.GreaterThanOrEqual, expression.Type);
                    break;
                case BinaryExpressionType.Lesser:
                    _result = WithCommonNumericType(left, right, L.Expression.LessThan, expression.Type);
                    break;
                case BinaryExpressionType.Greater:
                    _result = WithCommonNumericType(left, right, L.Expression.GreaterThan, expression.Type);
                    break;
                case BinaryExpressionType.Equal:
                    _result = WithCommonNumericType(left, right, L.Expression.Equal, expression.Type);
                    break;
                case BinaryExpressionType.Minus:
                    if (Checked) _result = WithCommonNumericType(left, right, L.Expression.SubtractChecked);
                    else _result = WithCommonNumericType(left, right, L.Expression.Subtract);
                    break;
                case BinaryExpressionType.Plus:
                    if (Checked) _result = WithCommonNumericType(left, right, L.Expression.AddChecked);
                    else _result = WithCommonNumericType(left, right, L.Expression.Add);
                    break;
                case BinaryExpressionType.Modulo:
                    _result = WithCommonNumericType(left, right, L.Expression.Modulo);
                    break;
                case BinaryExpressionType.Div:
                    _result = WithCommonNumericType(left, right, L.Expression.Divide);
                    break;
                case BinaryExpressionType.Times:
                    if (Checked) _result = WithCommonNumericType(left, right, L.Expression.MultiplyChecked);
                    else _result = WithCommonNumericType(left, right, L.Expression.Multiply);
                    break;
                case BinaryExpressionType.BitwiseOr:
                    _result = L.Expression.Or(left, right);
                    break;
                case BinaryExpressionType.BitwiseAnd:
                    _result = L.Expression.And(left, right);
                    break;
                case BinaryExpressionType.BitwiseXOr:
                    _result = L.Expression.ExclusiveOr(left, right);
                    break;
                case BinaryExpressionType.LeftShift:
                    _result = L.Expression.LeftShift(left, right);
                    break;
                case BinaryExpressionType.RightShift:
                    _result = L.Expression.RightShift(left, right);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public override void Visit(UnaryExpression expression)
        {
            expression.Expression.Accept(this);
            switch (expression.Type)
            {
                case UnaryExpressionType.Not:
                    _result = L.Expression.Not(_result);
                    break;
                case UnaryExpressionType.Negate:
                    _result = L.Expression.Negate(_result);
                    break;
                case UnaryExpressionType.BitwiseNot:
                    _result = L.Expression.Not(_result);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public override void Visit(ValueExpression expression)
        {
            _result = L.Expression.Constant(expression.Value);
        }

        public override void Visit(Function function)
        {
            var args = new L.Expression[function.Expressions.Length];
            for (int i = 0; i < function.Expressions.Length; i++)
            {
                function.Expressions[i].Accept(this);
                args[i] = _result;
            }

            switch (function.Identifier.Name.ToLowerInvariant())
            {
                case "if":
                    _result = L.Expression.Condition(args[0], args[1], args[2]);
                    break;
                case "in":
                    var items = L.Expression.NewArrayInit(args[0].Type,
                        new ArraySegment<L.Expression>(args, 1, args.Length - 1));
                    var smi = typeof (Array).GetRuntimeMethod("IndexOf", new[] { typeof(Array), typeof(object) });
                    var r = L.Expression.Call(smi, L.Expression.Convert(items, typeof(Array)), L.Expression.Convert(args[0], typeof(object)));
                    _result = L.Expression.GreaterThanOrEqual(r, L.Expression.Constant(0));
                    break;
                case "min":
                    var min_arg0 = L.Expression.Convert(args[0], typeof(double));
                    var min_arg1 = L.Expression.Convert(args[1], typeof(double));
                    _result = L.Expression.Condition(L.Expression.LessThan(min_arg0, min_arg1), min_arg0, min_arg1);
                    break;
                case "max":
                    var max_arg0 = L.Expression.Convert(args[0], typeof(double));
                    var max_arg1 = L.Expression.Convert(args[1], typeof(double));
                    _result = L.Expression.Condition(L.Expression.GreaterThan(max_arg0, max_arg1), max_arg0, max_arg1);
                    break;
                case "pow":
                    var pow_arg0 = L.Expression.Convert(args[0], typeof(double));
                    var pow_arg1 = L.Expression.Convert(args[1], typeof(double));
                    _result = L.Expression.Power(pow_arg0, pow_arg1);
                    break;
                default:
                    var mi = FindMethod(function.Identifier.Name, args);
                    _result = L.Expression.Call(_context, mi.BaseMethodInfo, mi.PreparedArguments);
                    break;
            }
        }

        public override void Visit(Identifier function)
        {
            if (_context == null)
            {
                _result = L.Expression.Constant(_parameters[function.Name]);
            }
            else
            {
                Exception forRethrow = null;
                try
                {
                    _result = L.Expression.PropertyOrField(_context, function.Name);
                }
                catch (ArgumentNullException e)
                {
                    if (!(AllowDerefMembers || AllowInvokeFunctions))
                    {
                        throw e;
                    }
                    forRethrow = e;
                }
                catch (ArgumentException e)
                {
                    if (!(AllowDerefMembers || AllowInvokeFunctions))
                    {
                        throw e;
                    }
                    forRethrow = e;
                }
                if (forRethrow != null)
                {
                    var obs = function.Name.SplitRegardingPrenthesis(0, -1, SRPOptions.TrimNRem, ".", false);
                    bool found = false;
                    L.Expression subContext = _context;
                    foreach (var ob in obs)
                    {
                        found = false;
                        var parts = ob.SplitRegardingPrenthesis(0, -1, SRPOptions.TrimNRem, "",true,"(",")");
                        if (parts.Count == 1)
                        {
                            if (!AllowDerefMembers)
                            {
                                throw forRethrow;
                            }
                            string subName = parts.First();
                            subContext = L.Expression.PropertyOrField(subContext, subName);
                            found = true;
                        }
                        else if (parts.Count == 2)
                        {
                            if (!AllowInvokeFunctions)
                            {
                                throw forRethrow;
                            }
                            string funcName = parts.First();
                            string argsStr = parts.Last();
                            var argsStrs = argsStr.SplitRegardingPrenthesis(0, -1, SRPOptions.TrimNRem, ",", false, "(", ")");
                            var args = argsStrs.Select(arg => new Expression(arg, EvaluateOptions.AllowLambdaParameterConversion).ToLambda<object>()()).ToArray();
                            var method = FindSubcontextMethod(subContext, funcName, args);
                            subContext = L.Expression.Call(subContext, method.BaseMethodInfo, method.PreparedArguments);
                            found = true;
                        }
                        else
                        {
                            throw forRethrow;
                        }
                    }
                    if (found)
                    {
                        _result = subContext;
                        forRethrow = null;
                    }
                    if (forRethrow != null)
                    {
                        throw forRethrow;
                    }
                }
            }
        }

        private ExtendedMethodInfo FindMethod(string methodName, L.Expression[] methodArgs, bool withConversion = false)
        {
            var methods = _context.Type.GetTypeInfo().DeclaredMethods.Where(m => m.Name.Equals(methodName, StringComparison.OrdinalIgnoreCase) && m.IsPublic && !m.IsStatic);
            foreach (var potentialMethod in methods)
            {
                var methodParams = potentialMethod.GetParameters();
                var newArguments = PrepareMethodArgumentsIfValid(methodParams, methodArgs, withConversion);

                if (newArguments != null)
                {
                    return new ExtendedMethodInfo() { BaseMethodInfo = potentialMethod, PreparedArguments = newArguments };
                }
            }
            if (AllowConversion && !withConversion)
            {
                return FindMethod(methodName, methodArgs, true);
            }
            throw new MissingMethodException($"method not found: {methodName}");
        }
        private ExtendedMethodInfo FindSubcontextMethod(L.Expression subContext, string methodName, object[] methodArgs, bool withConversion = false)
        {
            var methods = subContext.Type.GetTypeInfo().DeclaredMethods.Where(m => m.Name.Equals(methodName, StringComparison.OrdinalIgnoreCase) && m.IsPublic && !m.IsStatic);
            foreach (var potentialMethod in methods)
            {
                var methodParams = potentialMethod.GetParameters();
                var newArguments = PrepareSubcontextMethodArgumentsIfValid(methodParams, methodArgs, withConversion);

                if (newArguments != null)
                {
                    return new ExtendedMethodInfo() { BaseMethodInfo = potentialMethod, PreparedArguments = newArguments };
                }
            }
            if (AllowConversion && !withConversion)
            {
                return FindSubcontextMethod(subContext, methodName, methodArgs, true);
            }
            throw new MissingMethodException($"method not found: {methodName}");
        }

        private L.Expression[] PrepareSubcontextMethodArgumentsIfValid(ParameterInfo[] parameters, object[] arguments, bool withConversion)
        {
            if (!parameters.Any() && !arguments.Any()) return new L.Expression[] { };
            if (!parameters.Any()) return null;
            bool paramsMatchArguments = true;

            var lastParameter = parameters.Last();
            bool hasParamsKeyword = lastParameter.IsDefined(typeof(ParamArrayAttribute));
            if (hasParamsKeyword && parameters.Length > arguments.Length) return null;
            L.Expression[] newArguments = new L.Expression[parameters.Length];
            L.Expression[] paramsKeywordArgument = null;
            Type paramsElementType = null;
            int paramsParameterPosition = 0;
            if (!hasParamsKeyword)
            {
                paramsMatchArguments &= parameters.Length == arguments.Length;
                if (!paramsMatchArguments) return null;
            }
            else
            {
                paramsParameterPosition = lastParameter.Position;
                paramsElementType = lastParameter.ParameterType.GetElementType();
                paramsKeywordArgument = new L.Expression[arguments.Length - parameters.Length + 1];
            }

            for (int i = 0; i < arguments.Length; i++)
            {
                var isParamsElement = hasParamsKeyword && i >= paramsParameterPosition;
                var argumentType = arguments[i].GetType();
                var parameterType = isParamsElement ? paramsElementType : parameters[i].ParameterType;
                paramsMatchArguments &= argumentType == parameterType;
                L.Expression curArg = null;
                if (paramsMatchArguments)
                {
                    try
                    {
                        curArg = L.Expression.Convert(L.Expression.Constant(arguments[i]), argumentType);
                    }
                    catch (Exception)
                    {
                        curArg = null;
                    }
                }
                else //if (!paramsMatchArguments)
                {
                    if (withConversion)
                    {
                        try
                        {
                            curArg = L.Expression.Convert(L.Expression.Constant(arguments[i]), parameterType);
                            paramsMatchArguments = true;
                        }
                        catch (Exception)
                        {
                            paramsMatchArguments = false;
                        }
                    }
                    if (!paramsMatchArguments)
                    {
                        return null;
                    }
                }
                if (curArg == null)
                {
                    return null;
                }
                if (!isParamsElement)
                {
                    newArguments[i] = curArg;
                }
                else
                {
                    paramsKeywordArgument[i - paramsParameterPosition] = curArg;
                }
            }

            if (hasParamsKeyword)
            {
                newArguments[paramsParameterPosition] = L.Expression.NewArrayInit(paramsElementType, paramsKeywordArgument);
            }
            return newArguments;
        }
        private L.Expression[] PrepareMethodArgumentsIfValid(ParameterInfo[] parameters, L.Expression[] arguments, bool withConversion)
        {
            if (!parameters.Any() && !arguments.Any()) return arguments;
            if (!parameters.Any()) return null;
            bool paramsMatchArguments = true;

            var lastParameter = parameters.Last();
            bool hasParamsKeyword = lastParameter.IsDefined(typeof(ParamArrayAttribute));
            if (hasParamsKeyword && parameters.Length > arguments.Length) return null;
            L.Expression[] newArguments = new L.Expression[parameters.Length];
            L.Expression[] paramsKeywordArgument = null;
            Type paramsElementType = null;
            int paramsParameterPosition = 0;
            if (!hasParamsKeyword)
            {
                paramsMatchArguments &= parameters.Length == arguments.Length;
                if (!paramsMatchArguments) return null;
            }
            else
            {
                paramsParameterPosition = lastParameter.Position;
                paramsElementType = lastParameter.ParameterType.GetElementType();
                paramsKeywordArgument = new L.Expression[arguments.Length - parameters.Length + 1];
            }

            for (int i = 0; i < arguments.Length; i++)
            {
                var isParamsElement = hasParamsKeyword && i >= paramsParameterPosition;
                var argumentType = arguments[i].Type;
                var parameterType = isParamsElement ? paramsElementType : parameters[i].ParameterType;
                paramsMatchArguments &= argumentType == parameterType;
                var curArg = arguments[i];
                if (!paramsMatchArguments)
                {
                    if (withConversion)
                    {
                        try
                        {
                            curArg = (L.Expression)L.Expression.Convert(arguments[i], parameterType);
                            paramsMatchArguments = true;
                        }
                        catch (Exception)
                        {
                            paramsMatchArguments = false;
                        }
                    }
                    if (!paramsMatchArguments)
                    {
                        return null;
                    }
                }
                if (!isParamsElement)
                {
                    newArguments[i] = curArg;
                }
                else
                {
                    paramsKeywordArgument[i - paramsParameterPosition] = curArg;
                }
            }

            if (hasParamsKeyword)
            {
                newArguments[paramsParameterPosition] = L.Expression.NewArrayInit(paramsElementType, paramsKeywordArgument);
            }
            return newArguments;
        }

        private L.Expression WithCommonNumericType(L.Expression left, L.Expression right,
            Func<L.Expression, L.Expression, L.Expression> action, BinaryExpressionType expressiontype = BinaryExpressionType.Unknown)
        {
            left = UnwrapNullable(left);
            right = UnwrapNullable(right);

            if (_options.HasFlag(EvaluateOptions.BooleanCalculation))
            {
                if (left.Type == typeof(bool))
                {
                    left = L.Expression.Condition(left, L.Expression.Constant(1.0), L.Expression.Constant(0.0));
                }

                if (right.Type == typeof(bool))
                {
                    right = L.Expression.Condition(right, L.Expression.Constant(1.0), L.Expression.Constant(0.0));
                }
            }

            var precedence = new[]
            {
                typeof(decimal),
                typeof(double),
                typeof(float),
                typeof(ulong),
                typeof(long),
                typeof(uint),
                typeof(int),
                typeof(ushort),
                typeof(short),
                typeof(byte),
                typeof(sbyte)
            };

            int l = Array.IndexOf(precedence, left.Type);
            int r = Array.IndexOf(precedence, right.Type);
            if (l >= 0 && r >= 0)
            {
                var type = precedence[Math.Min(l, r)];
                if (left.Type != type)
                {
                    left = L.Expression.Convert(left, type);
                }

                if (right.Type != type)
                {
                    right = L.Expression.Convert(right, type);
                }
            }
            L.Expression comparer = null;
            if (IgnoreCaseString)
            {
                if (Ordinal) comparer = L.Expression.Property(null, typeof(StringComparer), "OrdinalIgnoreCase");
                else comparer = L.Expression.Property(null, typeof(StringComparer), "CurrentCultureIgnoreCase");
            }
            else comparer = L.Expression.Property(null, typeof(StringComparer), "Ordinal");

            if (comparer != null && (typeof(string).Equals(left.Type) || typeof(string).Equals(right.Type)))
            {
                switch (expressiontype)
                {
                    case BinaryExpressionType.Equal: return L.Expression.Call(comparer, typeof(StringComparer).GetRuntimeMethod("Equals", new[] { typeof(string), typeof(string) }), new L.Expression[] { left, right });
                    case BinaryExpressionType.NotEqual: return L.Expression.Not(L.Expression.Call(comparer, typeof(StringComparer).GetRuntimeMethod("Equals", new[] { typeof(string), typeof(string) }), new L.Expression[] { left, right }));
                    case BinaryExpressionType.GreaterOrEqual: return L.Expression.GreaterThanOrEqual(L.Expression.Call(comparer, typeof(StringComparer).GetRuntimeMethod("Compare", new[] { typeof(string), typeof(string) }), new L.Expression[] { left, right }), L.Expression.Constant(0));
                    case BinaryExpressionType.LesserOrEqual: return L.Expression.LessThanOrEqual(L.Expression.Call(comparer, typeof(StringComparer).GetRuntimeMethod("Compare", new[] { typeof(string), typeof(string) }), new L.Expression[] { left, right }), L.Expression.Constant(0));
                    case BinaryExpressionType.Greater: return L.Expression.GreaterThan(L.Expression.Call(comparer, typeof(StringComparer).GetRuntimeMethod("Compare", new[] { typeof(string), typeof(string) }), new L.Expression[] { left, right }), L.Expression.Constant(0));
                    case BinaryExpressionType.Lesser: return L.Expression.LessThan(L.Expression.Call(comparer, typeof(StringComparer).GetRuntimeMethod("Compare", new[] { typeof(string), typeof(string) }), new L.Expression[] { left, right }), L.Expression.Constant(0));
                }
            }
            return action(left, right);
        }

        private L.Expression UnwrapNullable(L.Expression expression)
        {
            var ti = expression.Type.GetTypeInfo();
            if (ti.IsGenericType && ti.GetGenericTypeDefinition() == typeof (Nullable<>))
            {
                return L.Expression.Condition(
                    L.Expression.Property(expression, "HasValue"),
                    L.Expression.Property(expression, "Value"),
                    L.Expression.Default(expression.Type.GetTypeInfo().GenericTypeArguments[0]));
            }

            return expression;
        }
    }
}
