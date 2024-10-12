The official [MiniSat page](http://minisat.se/MiniSat.html) links to a C# port, but the link is dead.
I've recovered the source code (although not, alas, the tests) with the aid of archive.org, and that forms the first commit.
I intend to make some improvements, because there is room for speed and memory optimisation.


## CNF Input Format

CNF stands for Conjunctive Normal Form.

The executable takes as input files encoding CNF in the format described below.

### Comments
Comments are lines that start with `c`

### Problem
The first non-comment line is in the format `p <problem type> <number of variables> <number of clauses>`.

For CNF, it must start with `p cnf`.

### Clauses
- A clause is a sequence of whitespace-separated variables, terminated with `0`.
- A clause may span multiple lines.
- Variables are referred to by their 1-based index (since `0` is used for clause termination).
- Variables can be positive or negative.

### Interpretation

Clauses are conjoined (`AND(c1, c2)`), while variables in clauses are disjoined (`OR(v1, v2)`).

Variables can be positive (`v`) or negative (`NOT(v)`).

### A worked example

``` txt
c  simple_v3_c2.cnf
c
p cnf 3 2
1 -3 0
2 3 -1 0
```

The problem statement in the above example can be interpreted as (with PowerShell syntax):

``` powershell
# simple_v3_c2.cnf
#

$problemType = "CNF"
$numVariables = 3
$numClauses = 2

$v1 = $true
$v2 = $true
$v3 = $true

$clause1 = $v1 -or (-not $v3)
$clause2 = $v2 -or $v3 -or (-not $v1)

$clause1 -and $clause2
```
