using Microsoft.Dynamics.Nav.CodeAnalysis;
using System;
using System.Collections.Generic;
using TFaller.ALTools.Transformation.Transformer.CommentRule;

namespace TFaller.ALTools.Transformation.Tests;

public class QuerySyncTests
{
    [Theory]
    [InlineData("Name", "Name")]
    [InlineData("No.", "No")]
    [InlineData("Sell-to Customer No.", "SellToCustomerNo")]
    [InlineData("Customer Name", "CustomerName")]
    [InlineData("Field 1", "Field1")]
    [InlineData("My_Field", "MyField")]
    [InlineData("123Start", "F123Start")]
    public void MakeColumnIdentifierTest(string fieldName, string expected)
    {
        Assert.Equal(expected, QuerySync.MakeColumnIdentifier(fieldName));
    }

    // Basic: query with empty data item gets all table fields added as columns
    [Theory]
    [InlineData(
        """
        table 1 "My Table"
        {
            fields
            {
                field(1; "No."; Code[20]) { }
                field(2; Name; Text[100]) { }
            }
        }

        // @altools:transform:sync:query-sync:My Table
        query 1 MyQuery
        {
            elements
            {
                dataitem(MyTable; "My Table")
                {
                }
            }
        }
        """,
        """
        table 1 "My Table"
        {
            fields
            {
                field(1; "No."; Code[20]) { }
                field(2; Name; Text[100]) { }
            }
        }

        // @altools:transform:sync:query-sync:My Table
        query 1 MyQuery
        {
            elements
            {
                dataitem(MyTable; "My Table")
                {
                    column(No; "No.") { }
                    column(Name; Name) { }
                }
            }
        }
        """,
        "sync"
    )]
    // Partial sync: some columns already exist, only missing ones are added
    [InlineData(
        """
        table 1 "My Table"
        {
            fields
            {
                field(1; "No."; Code[20]) { }
                field(2; Name; Text[100]) { }
                field(3; Amount; Decimal) { }
            }
        }

        // @altools:transform:sync:query-sync:My Table
        query 1 MyQuery
        {
            elements
            {
                dataitem(MyTable; "My Table")
                {
                    column(No; "No.") { }
                }
            }
        }
        """,
        """
        table 1 "My Table"
        {
            fields
            {
                field(1; "No."; Code[20]) { }
                field(2; Name; Text[100]) { }
                field(3; Amount; Decimal) { }
            }
        }

        // @altools:transform:sync:query-sync:My Table
        query 1 MyQuery
        {
            elements
            {
                dataitem(MyTable; "My Table")
                {
                    column(No; "No.") { }
                    column(Name; Name) { }
                    column(Amount; Amount) { }
                }
            }
        }
        """,
        "sync"
    )]
    // Already in sync: no changes when all columns already exist
    [InlineData(
        """
        table 1 "My Table"
        {
            fields
            {
                field(1; "No."; Code[20]) { }
                field(2; Name; Text[100]) { }
            }
        }

        // @altools:transform:sync:query-sync:My Table
        query 1 MyQuery
        {
            elements
            {
                dataitem(MyTable; "My Table")
                {
                    column(No; "No.") { }
                    column(Name; Name) { }
                }
            }
        }
        """,
        """
        table 1 "My Table"
        {
            fields
            {
                field(1; "No."; Code[20]) { }
                field(2; Name; Text[100]) { }
            }
        }

        // @altools:transform:sync:query-sync:My Table
        query 1 MyQuery
        {
            elements
            {
                dataitem(MyTable; "My Table")
                {
                    column(No; "No.") { }
                    column(Name; Name) { }
                }
            }
        }
        """,
        "sync"
    )]
    // FlowField: FlowField is excluded automatically
    [InlineData(
        """
        table 1 "My Table"
        {
            fields
            {
                field(1; "No."; Code[20]) { }
                field(2; Balance; Decimal)
                {
                    FieldClass = FlowField;
                    CalcFormula = sum("Entry"."Amount");
                }
            }
        }

        // @altools:transform:sync:query-sync:My Table
        query 1 MyQuery
        {
            elements
            {
                dataitem(MyTable; "My Table")
                {
                }
            }
        }
        """,
        """
        table 1 "My Table"
        {
            fields
            {
                field(1; "No."; Code[20]) { }
                field(2; Balance; Decimal)
                {
                    FieldClass = FlowField;
                    CalcFormula = sum("Entry"."Amount");
                }
            }
        }

        // @altools:transform:sync:query-sync:My Table
        query 1 MyQuery
        {
            elements
            {
                dataitem(MyTable; "My Table")
                {
                    column(No; "No.") { }
                }
            }
        }
        """,
        "sync"
    )]
    // FlowFilter: FlowFilter is excluded automatically
    [InlineData(
        """
        table 1 "My Table"
        {
            fields
            {
                field(1; "No."; Code[20]) { }
                field(2; "Date Filter"; Date)
                {
                    FieldClass = FlowFilter;
                }
            }
        }

        // @altools:transform:sync:query-sync:My Table
        query 1 MyQuery
        {
            elements
            {
                dataitem(MyTable; "My Table")
                {
                }
            }
        }
        """,
        """
        table 1 "My Table"
        {
            fields
            {
                field(1; "No."; Code[20]) { }
                field(2; "Date Filter"; Date)
                {
                    FieldClass = FlowFilter;
                }
            }
        }

        // @altools:transform:sync:query-sync:My Table
        query 1 MyQuery
        {
            elements
            {
                dataitem(MyTable; "My Table")
                {
                    column(No; "No.") { }
                }
            }
        }
        """,
        "sync"
    )]
    // Disabled field: field with Enabled = false is excluded
    [InlineData(
        """
        table 1 "My Table"
        {
            fields
            {
                field(1; "No."; Code[20]) { }
                field(2; Legacy; Text[50])
                {
                    Enabled = false;
                }
            }
        }

        // @altools:transform:sync:query-sync:My Table
        query 1 MyQuery
        {
            elements
            {
                dataitem(MyTable; "My Table")
                {
                }
            }
        }
        """,
        """
        table 1 "My Table"
        {
            fields
            {
                field(1; "No."; Code[20]) { }
                field(2; Legacy; Text[50])
                {
                    Enabled = false;
                }
            }
        }

        // @altools:transform:sync:query-sync:My Table
        query 1 MyQuery
        {
            elements
            {
                dataitem(MyTable; "My Table")
                {
                    column(No; "No.") { }
                }
            }
        }
        """,
        "sync"
    )]
    // Blacklist: fields listed in annotation blacklist are excluded
    [InlineData(
        """
        table 1 "My Table"
        {
            fields
            {
                field(1; "No."; Code[20]) { }
                field(2; Name; Text[100]) { }
                field(3; Amount; Decimal) { }
            }
        }

        // @altools:transform:sync:query-sync:My Table
        // - Name
        // - Amount
        query 1 MyQuery
        {
            elements
            {
                dataitem(MyTable; "My Table")
                {
                }
            }
        }
        """,
        """
        table 1 "My Table"
        {
            fields
            {
                field(1; "No."; Code[20]) { }
                field(2; Name; Text[100]) { }
                field(3; Amount; Decimal) { }
            }
        }

        // @altools:transform:sync:query-sync:My Table
        // - Name
        // - Amount
        query 1 MyQuery
        {
            elements
            {
                dataitem(MyTable; "My Table")
                {
                    column(No; "No.") { }
                }
            }
        }
        """,
        "sync"
    )]
    // Quoted blacklist entry
    [InlineData(
        """
        table 1 "My Table"
        {
            fields
            {
                field(1; "No."; Code[20]) { }
                field(2; Name; Text[100]) { }
            }
        }

        // @altools:transform:sync:query-sync:My Table
        // - "No."
        query 1 MyQuery
        {
            elements
            {
                dataitem(MyTable; "My Table")
                {
                }
            }
        }
        """,
        """
        table 1 "My Table"
        {
            fields
            {
                field(1; "No."; Code[20]) { }
                field(2; Name; Text[100]) { }
            }
        }

        // @altools:transform:sync:query-sync:My Table
        // - "No."
        query 1 MyQuery
        {
            elements
            {
                dataitem(MyTable; "My Table")
                {
                    column(Name; Name) { }
                }
            }
        }
        """,
        "sync"
    )]
    // Inactive tag: query with non-matching tag is not transformed
    [InlineData(
        """
        table 1 "My Table"
        {
            fields
            {
                field(1; "No."; Code[20]) { }
            }
        }

        // @altools:transform:cloud:query-sync:My Table
        query 1 MyQuery
        {
            elements
            {
                dataitem(MyTable; "My Table")
                {
                }
            }
        }
        """,
        """
        table 1 "My Table"
        {
            fields
            {
                field(1; "No."; Code[20]) { }
            }
        }

        // @altools:transform:cloud:query-sync:My Table
        query 1 MyQuery
        {
            elements
            {
                dataitem(MyTable; "My Table")
                {
                }
            }
        }
        """,
        "onprem"
    )]
    // No tag filter: when no active tags are set, all annotations match
    [InlineData(
        """
        table 1 "My Table"
        {
            fields
            {
                field(1; "No."; Code[20]) { }
            }
        }

        // @altools:transform:cloud:query-sync:My Table
        query 1 MyQuery
        {
            elements
            {
                dataitem(MyTable; "My Table")
                {
                }
            }
        }
        """,
        """
        table 1 "My Table"
        {
            fields
            {
                field(1; "No."; Code[20]) { }
            }
        }

        // @altools:transform:cloud:query-sync:My Table
        query 1 MyQuery
        {
            elements
            {
                dataitem(MyTable; "My Table")
                {
                    column(No; "No.") { }
                }
            }
        }
        """,
        null
    )]
    // No annotation: query without annotation is unchanged
    [InlineData(
        """
        table 1 "My Table"
        {
            fields
            {
                field(1; "No."; Code[20]) { }
            }
        }

        query 1 MyQuery
        {
            elements
            {
                dataitem(MyTable; "My Table")
                {
                }
            }
        }
        """,
        """
        table 1 "My Table"
        {
            fields
            {
                field(1; "No."; Code[20]) { }
            }
        }

        query 1 MyQuery
        {
            elements
            {
                dataitem(MyTable; "My Table")
                {
                }
            }
        }
        """,
        "sync"
    )]
    public void QuerySyncTransformTest(string input, string expected, string? tags)
    {
        var compilationUnit = SyntaxFactory.ParseCompilationUnit(input);
        var compilation = Compilation.Create("temp").AddSyntaxTrees(compilationUnit.SyntaxTree);
        var model = compilation.GetSemanticModel(compilationUnit.SyntaxTree);

        HashSet<string>? activeTags = null;
        if (tags != null)
        {
            activeTags = new HashSet<string>(
                tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                StringComparer.OrdinalIgnoreCase);
        }

        var rewriter = new QuerySync(activeTags);
        var context = rewriter.EmptyContext.WithModel(model);
        var result = rewriter.Rewrite(compilationUnit, ref context);

        Assert.Equal(expected, result.ToFullString(), ignoreAllWhiteSpace: true);
    }
}
