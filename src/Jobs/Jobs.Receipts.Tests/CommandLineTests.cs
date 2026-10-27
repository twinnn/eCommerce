using Jobs.Receipts.Apply;

namespace Jobs.Receipts.Tests;

public class CommandLineTests
{
    [Fact]
    public void Parse_returns_Usage_for_no_arguments()
    {
        var command = CommandLine.Parse([]);

        Assert.Equal(ReceiptsCommandKind.Usage, command.Kind);
        Assert.Null(command.Error);
    }

    [Fact]
    public void Parse_returns_Usage_for_an_unknown_command()
    {
        Assert.Equal(ReceiptsCommandKind.Usage, CommandLine.Parse(["scan"]).Kind);
    }

    [Fact]
    public void Parse_reads_apply_with_an_id()
    {
        var command = CommandLine.Parse(["apply", "--id", "abc123"]);

        Assert.Equal(ReceiptsCommandKind.Apply, command.Kind);
        Assert.Equal("abc123", command.Id);
        Assert.False(command.All);
        Assert.Null(command.Error);
    }

    [Fact]
    public void Parse_reads_apply_with_all()
    {
        var command = CommandLine.Parse(["apply", "--all"]);

        Assert.Equal(ReceiptsCommandKind.Apply, command.Kind);
        Assert.True(command.All);
        Assert.Null(command.Id);
        Assert.Null(command.Error);
    }

    [Fact]
    public void Parse_prefers_all_when_both_all_and_id_are_given()
    {
        var command = CommandLine.Parse(["apply", "--all", "--id", "abc123"]);

        Assert.True(command.All);
        Assert.Null(command.Id);
    }

    [Fact]
    public void Parse_reports_an_error_when_apply_has_neither_id_nor_all()
    {
        var command = CommandLine.Parse(["apply"]);

        Assert.Equal(ReceiptsCommandKind.Apply, command.Kind);
        Assert.Equal("Použi 'apply --id <id>' alebo 'apply --all'.", command.Error);
    }

    [Theory]
    [InlineData("apply", "Použi 'apply --id <id>' alebo 'apply --all'.")]
    [InlineData("reset", "Použi 'reset --id <id>'.")]
    public void Parse_reports_an_error_when_id_has_no_value(string verb, string expectedError)
    {
        var command = CommandLine.Parse([verb, "--id"]);

        Assert.Equal(expectedError, command.Error);
        Assert.Null(command.Id);
    }

    [Fact]
    public void Parse_reads_status()
    {
        var command = CommandLine.Parse(["status"]);

        Assert.Equal(ReceiptsCommandKind.Status, command.Kind);
        Assert.Null(command.Error);
    }

    [Fact]
    public void Parse_reads_reset_with_an_id()
    {
        var command = CommandLine.Parse(["reset", "--id", "abc123"]);

        Assert.Equal(ReceiptsCommandKind.Reset, command.Kind);
        Assert.Equal("abc123", command.Id);
        Assert.Null(command.Error);
    }

    [Fact]
    public void Parse_reports_an_error_when_reset_has_no_id_at_all()
    {
        var command = CommandLine.Parse(["reset"]);

        Assert.Equal(ReceiptsCommandKind.Reset, command.Kind);
        Assert.Equal("Použi 'reset --id <id>'.", command.Error);
    }

    [Fact]
    public void Usage_lists_every_supported_command()
    {
        Assert.Contains("apply --id <id>", CommandLine.Usage);
        Assert.Contains("apply --all", CommandLine.Usage);
        Assert.Contains("status", CommandLine.Usage);
        Assert.Contains("reset --id <id>", CommandLine.Usage);
    }
}
