module Unicorn.Compiler.Ast

type Id = Id of string
and IdPath = IdPath of Id list
and TypedId = TypedId of id: Id * type': SignatureExpression
and String = String of string

and Op =
    | Add
    | Subtract
    | Multiply
    | Divide
    | Degree
    | Pipe
    | Compose

and Primary =
    | Id of Id
    | IdPath of IdPath
    | Character of char
    | Integer of int
    | String of String
    | Boolean of bool
    | List of Expression list

and Expression =
    | Binary of Expression * Op * Expression
    | Unary of Op * Expression
    | Primary of Primary
    | FunctionCall of name: IdPath * arguments: Expression list

and SignatureExpression =
    | Tuple of SignatureExpression * SignatureExpression
    | Arrow of SignatureExpression * SignatureExpression
    | Type of IdPath

type LetBinding =
    | Value of isMutable: bool * id: TypedId * value: ExpressionStatement option
    | Function of name: Id * params': TypedId list * returns: SignatureExpression * body: ExpressionStatement

and ExpressionStatement =
    | LetBinding of LetBinding
    | PatternMatch of value: Expression * cases: (PatternExpression * ExpressionStatement) list
    | Assignment of id: IdPath * value: ExpressionStatement
    | Expression of Expression
    | Multiple of ExpressionStatement list

and PatternExpression =
    | UnionDeconstruct of union: IdPath * value: UnionDeconstructValue
    | TupleDeconstruct of values: (Id list)
    | Primary of Primary

and UnionDeconstructValue =
    | Tuple of values: (Id list)
    | Single of Id
    | Empty

and UnionCase =
    | Typed of TypedId
    | Untyped of Id

and TypeDecl =
    | Data of name: Id * fields: TypedId list
    | Union of name: Id * cases: UnionCase list
    | SingleUnion of TypedId
    | Alias of name: Id * for': IdPath

and InModuleDecl =
    | Open of IdPath
    | TypeDecl of TypeDecl
    | LetBinding of LetBinding

and TopLevelStatement =
    | ModuleDef of name: IdPath * body: InModuleDecl list