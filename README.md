# NCalc2 clone
Clone of the CoreCLR-NCalc which in turn is a clone of NCalc from http://ncalc.codeplex.com/ 
This cloane fixes sime issues I needed fixed for my uses (e.g. allowing type conversion in function parameter search so that double and int anc be used interchangebly so that integer literals can be used in double calls without adding ".0") and allows to some degree access to lmbda context object members and member methods.
at this stage accessign members of members is possible and also members of returned function results, but function call parameters are just IConver()ed from string

## Expressions with Functions and Parameters

```csharp
class ExpressionContext
{
  public int Param1 { get; set; }
  public string Param2 { get; set; }
  public class CCC
  {
    public int ccc = 5;
    public double funcA() { return 22.2;}
  }
  public CCC C = new CCC();
  public double Foo(double a, double b, double c)
  {
    return a + b + c;
  }
}

var expr = new Expression("Foo([Param1], C.ccc, c.funcA()) = 29.2 && [Param2] = 'test'");
Func<ExpressionContext, bool> f = expr.ToLambda<ExpressionContext, bool>();

var context = new ExpressionContext { Param1 = 2, Param2 = "test" };
Console.WriteLine(f(context)); // will print True
```
