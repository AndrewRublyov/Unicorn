module Unicorn.Parser

open FParsec
open System

open Unicorn.ParserHelpers
open Errors

let getGuid = Guid.NewGuid

// Keywords
let private LET             = "let"
let private FUNC            = "func"
let private IF              = "if"
let private ELSE            = "else"
let private WHILE           = "while"
let private RETURN          = "return"
let private BREAK           = "break"

// Type names
let private NONE            = "none"
let private STRING          = "string"
let private BOOL            = "bool"
let private INT             = "int"
let private DOUBLE          = "double"

// Comments
let private COMMENT_START  = '#'

// Literals
let private STRING_LIT      = @"\""(([^\""]|\\\"")*[^\\])?\""|\'(([^\""]|\\\"")*[^\\])?\'"
let private INT_LIT         = @"\d+"
let private DOUBLE_LIT      = @"\d*\.\d+"
let private TRUE_LIT        = "true"
let private FALSE_LIT       = "false"

// Operators
let private PLUS            = "+"
let private MINUS           = "-"
let private NOT             = "not"
let private ASTERISK        = "*"
let private DOUBLEASTERISK  = "**"
let private PERCENT         = "%"
let private FORWARDSLASH    = "/"
let private SINGLEEQUALS    = "="
let private OR              = "or"
let private AND             = "and"
let private IS              = "is"
let private EQ              = "="
let private LTEQ            = "<="
let private LT              = "<"
let private GTEQ            = ">="
let private GT              = ">"

// Common
let private IDENTIFIER      = "[a-zA-Z_\$][a-zA-Z_\$0-9]*"
let private DOT             = "."
let private OPENPAREN       = "("
let private CLOSEPAREN      = ")"
let private OPENCURLY       = "{"
let private CLOSECURLY      = "}"
let private OPENSQUARE      = "["
let private CLOSESQUARE     = "]"
let private COLON           = ":"
let private COMMA           = ","

let typeSpec : Parser<Ast.TypeSpec, unit> =
    choice_ws [
        attempt (keyword NONE    |>> (fun _ -> Ast.NoneType)) ;
        attempt (keyword STRING  |>> (fun _ -> Ast.String)) ;
        attempt (keyword INT     |>> (fun _ -> Ast.Int)) ;
        attempt (keyword DOUBLE  |>> (fun _ -> Ast.Double)) ;
                (keyword BOOL    |>> (fun _ -> Ast.Bool)) ;
    ]

let identifier : Parser<Ast.Identifier, unit> =
    regex_ws IDENTIFIER |>> (fun a -> string a)

/// Literals

let stringLiteral : Parser<Ast.Literal, unit> =
    literal STRING_LIT
        |>> (fun s -> Ast.StringLiteral((s.Substring(1, s.Length - 2))))

let boolLiteral : Parser<Ast.Literal, unit> =
    let trueLiteral = str_ws TRUE_LIT
                        |>> (fun _ -> Ast.BoolLiteral true)

    let falseLiteral = str_ws FALSE_LIT
                        |>> (fun _ -> Ast.BoolLiteral false)

    trueLiteral <|> falseLiteral

let intLiteral : Parser<Ast.Literal, unit>  =
    literal INT_LIT
        |>> (fun a -> Ast.IntLiteral (int a))

let doubleLiteral : Parser<Ast.Literal, unit> =
    literal DOUBLE_LIT
        |>> (fun a -> Ast.DoubleLiteral (double a))

let literal : Parser<Ast.Literal, unit> =
    choice_ws [
        attempt stringLiteral ;
        attempt boolLiteral ;
        attempt doubleLiteral ;
                intLiteral ;
    ]



/// Expressions

let expression, expressionImpl = createParserForwardedToRef()

let literalExpression : Parser<Ast.Expression, unit> =
    literal
        |>> (fun a -> Ast.LiteralExpression (a, getGuid()))

let assignmentExpression : Parser<Ast.Expression, unit> =
    (pipe2 (identifier) (symbol EQ >>. expression)
        (fun a b -> Ast.VariableAssignmentExpression ({ Identifier = a; Guid = getGuid() }, b, getGuid()))) ;

let arguments : Parser<Ast.Arguments, unit> = sepBy_ws expression (symbol COMMA)

let identifierExpression : Parser<Ast.Expression, unit> =
    choice_ws [
        attempt (pipe2 (identifier) (symbol OPENPAREN >>. arguments .>> symbol CLOSEPAREN)
            (fun a b -> Ast.FunctionCallExpression (a, b, getGuid()))) ;
        identifier |>> (fun x -> Ast.IdentifierExpression ({ Identifier = x; Guid = getGuid(); }, getGuid())) ;
    ]

/// Operators

let opp = new OperatorPrecedenceParser<Ast.Expression, unit, unit>()
let termsExpression = opp.ExpressionParser

let termParser =
    choice_ws [
        attempt assignmentExpression ;
        attempt literalExpression ;
        attempt identifierExpression ;
        between (symbol OPENPAREN) (symbol CLOSEPAREN) termsExpression
    ]

let identifierFromExpression = function
    | Ast.IdentifierExpression (i, _) -> {i with Guid = getGuid()}
    | _ -> raise (syntaxError (sprintf "Identifier expected"))

opp.TermParser <- termParser

opp.AddOperator(InfixOperator(OR, ws, 1, Associativity.Left, fun x y ->     binary x Ast.Eq y (getGuid())))
opp.AddOperator(InfixOperator(IS, ws, 2, Associativity.Left, fun x y ->     binary x Ast.Eq y (getGuid())))

opp.AddOperator(InfixOperator(LTEQ, ws, 2, Associativity.Left, fun x y ->   binary x Ast.LtEq y (getGuid())))
opp.AddOperator(InfixOperator(LT, ws, 2, Associativity.Left, fun x y ->     binary x Ast.Lt y (getGuid())))

opp.AddOperator(InfixOperator(GTEQ, ws, 2, Associativity.Left, fun x y ->   binary x Ast.GtEq y (getGuid())))
opp.AddOperator(InfixOperator(GT, ws, 2, Associativity.Left, fun x y ->     binary x Ast.Gt y (getGuid())))

opp.AddOperator(InfixOperator(AND, ws, 3, Associativity.Left, fun x y ->    binary x Ast.And y (getGuid())))

opp.AddOperator(InfixOperator(PLUS, ws, 1, Associativity.Left, fun x y ->            binary x Ast.Sum y (getGuid())))
opp.AddOperator(InfixOperator(MINUS, ws, 1, Associativity.Left, fun x y ->           binary x Ast.Diff y (getGuid())))
opp.AddOperator(InfixOperator(ASTERISK, ws, 2, Associativity.Left, fun x y ->        binary x Ast.Mult y (getGuid())))
opp.AddOperator(InfixOperator(FORWARDSLASH, ws, 2, Associativity.Left, fun x y ->    binary x Ast.Div y (getGuid())))
opp.AddOperator(InfixOperator(DOUBLEASTERISK, ws, 3, Associativity.Right, fun x y -> binary x Ast.Pow y (getGuid())))

opp.AddOperator(PrefixOperator(NOT, ws, 4, true, fun x -> (unary x Ast.Not (getGuid()))))
opp.AddOperator(PrefixOperator(MINUS, ws, 4, true, fun x -> (unary x Ast.Minus (getGuid()))))
opp.AddOperator(PrefixOperator(PLUS, ws, 4, true, fun x -> (unary x Ast.Plus (getGuid()))))

do expressionImpl :=
    choice_ws [
        attempt assignmentExpression ;
        attempt termsExpression ;
        attempt literalExpression ;
        identifierExpression ;
    ]

/// Statements

let statement, statementImpl = createParserForwardedToRef()

let statements : Parser<Ast.Statement list, unit> = many_ws statement

let breakStatement : Parser<Ast.Statement, unit> =
    (keyword BREAK |>> (fun _ -> Ast.BreakStatement))

let returnStatement : Parser<Ast.Statement, unit> =
    choice_ws [
        attempt (keyword RETURN >>. expression |>> (fun a -> Ast.ReturnStatement (Some a))) ;
                (keyword RETURN |>> (fun _ -> Ast.ReturnStatement None)) ;
    ]

let blockStatement =
    symbol OPENCURLY >>. statements .>> symbol CLOSECURLY |>>
        (fun a -> Ast.BlockStatement (a))

let ifStatement : Parser<Ast.Statement, unit> =
    choice_ws [
        attempt (pipe3 (keyword IF >>. symbol OPENPAREN >>. expression .>> symbol CLOSEPAREN) (statement) (keyword ELSE >>. statement)
            (fun a b c -> Ast.IfStatement (a, b, Some c))) ;
        (pipe2 (keyword IF >>. symbol OPENPAREN >>. expression .>> symbol CLOSEPAREN) (statement)
            (fun a b -> Ast.IfStatement (a, b, None)));
    ]

let whileStatement : Parser<Ast.Statement, unit> =
    pipe2 (keyword WHILE >>. symbol OPENPAREN >>. expression .>> symbol CLOSEPAREN) (statement)
        (fun a b -> Ast.WhileStatement (a, b))

let expressionStatement : Parser<Ast.Statement, unit> =
    expression |>> (fun a -> Ast.ExpressionStatement (a))


let parameterStatement : Parser<Ast.VariableDeclarationStatement, unit> =
        (pipe2 (identifier) (symbol COLON >>. typeSpec)
            (fun a b -> (a, b, getGuid()))) ;

let parametersStatement : Parser<Ast.Parameters, unit> = sepBy_ws parameterStatement (symbol COMMA)

let functionDeclarationStatement : Parser<Ast.Statement, unit> =
    choice_ws [
        attempt (pipe4 (keyword FUNC >>. identifier) (symbol OPENPAREN >>. parametersStatement .>> symbol CLOSEPAREN)
            (symbol COLON >>. typeSpec) (blockStatement)
            (fun a b c d -> Ast.FunctionDeclarationStatement (a, b, c, d, getGuid()))) ;
        (pipe3 (keyword FUNC >>. identifier) (symbol OPENPAREN >>. parametersStatement .>> symbol CLOSEPAREN)
            (blockStatement)
            (fun a b c -> Ast.FunctionDeclarationStatement (a, b, Ast.NoneType, c, getGuid()))) ;
    ]

let variableDeclarationStatement : Parser<Ast.Statement, unit> =
    (pipe2 (keyword LET >>. identifier) (symbol COLON >>. typeSpec)
        (fun a b -> Ast.VariableDeclarationStatement (a, b, getGuid()))) ;

/// Comments

let singleLineComment : Parser<Ast.Statement, unit> =
    (pchar COMMENT_START >>. restOfLine true) |>> (fun x -> Ast.CommentStatement x)

do statementImpl :=
    choice_ws [
        attempt singleLineComment ;
        attempt functionDeclarationStatement ;
        attempt variableDeclarationStatement ;
        attempt ifStatement ;
        attempt whileStatement;
        attempt returnStatement ;
        attempt breakStatement ;
        attempt blockStatement ;
        expressionStatement ;
    ]

/// Declarations

let declarationStatement =
    choice_ws [
        variableDeclarationStatement ;
        functionDeclarationStatement ;
        singleLineComment ;
    ]

let declarationStatementList = many_ws declarationStatement

let program = declarationStatementList .>> eof

let parse (input : string) =
    match run program input with
    | Success(result, _, _)   -> Result.Ok result
    | Failure(errorMsg, _, _) -> Result.Error (syntaxError errorMsg)