using AutomatonEditor.Core;

namespace AutomatonEditor.Core.Tests;

public class DfaValidatorTests
{
    [Fact]
    public void Builds_a_well_formed_dfa()
    {
        var ok = DfaValidator.TryBuild(Fixtures.EvenZeros(), out var dfa, out var problems);

        Assert.True(ok, string.Join("; ", problems.Select(p => p.Message)));
        Assert.NotNull(dfa);
        Assert.Equal("q0", dfa!.InitialState);
        Assert.Equal(['0', '1'], dfa.Alphabet);
        Assert.Equal(4, dfa.TransitionCount);
        Assert.True(dfa.IsComplete);
        Assert.Empty(dfa.UnreachableStates());
    }

    [Fact]
    public void Rejects_a_document_without_states()
    {
        var problems = DfaValidator.Validate(new DfaSaveData());
        Assert.Contains(problems, p => p.Kind == DfaProblemKind.NoStates);
    }

    [Fact]
    public void Requires_exactly_one_initial_state()
    {
        var none = Fixtures.EvenZeros();
        foreach (var s in none.States!) s.IsInitial = false;
        Assert.Contains(DfaValidator.Validate(none), p => p.Kind == DfaProblemKind.NoInitialState);

        var two = Fixtures.EvenZeros();
        foreach (var s in two.States!) s.IsInitial = true;
        Assert.Contains(DfaValidator.Validate(two), p => p.Kind == DfaProblemKind.MultipleInitialStates);
    }

    [Fact]
    public void Rejects_non_determinism()
    {
        var data = Fixtures.EvenZeros();
        data.Transitions = [.. data.Transitions!, new TransitionSaveData { FromId = "q0", ToId = "q0", Symbol = "0" }];

        var problems = DfaValidator.Validate(data);

        var problem = Assert.Single(problems);
        Assert.Equal(DfaProblemKind.NonDeterministic, problem.Kind);
        Assert.Contains("'0'", problem.Message);
    }

    [Fact]
    public void Rejects_multi_character_and_empty_symbols()
    {
        var data = Fixtures.EvenZeros();
        data.Transitions![0].Symbol = "00";
        data.Transitions[1].Symbol = " , ";

        var problems = DfaValidator.Validate(data);

        Assert.Contains(problems, p => p.Kind == DfaProblemKind.SymbolNotSingleCharacter);
        Assert.Contains(problems, p => p.Kind == DfaProblemKind.EmptySymbol);
    }

    [Fact]
    public void Rejects_dangling_transitions_and_duplicate_ids()
    {
        var data = Fixtures.EvenZeros();
        data.Transitions![0].ToId = "ghost";
        data.States![1].Id = "q0";

        var kinds = DfaValidator.Validate(data).Select(p => p.Kind).ToList();

        Assert.Contains(DfaProblemKind.UnknownState, kinds);
        Assert.Contains(DfaProblemKind.DuplicateStateId, kinds);
    }

    [Fact]
    public void Reports_unreachable_states_and_incomplete_transition_functions()
    {
        var data = Fixtures.EvenZeros();
        data.States = [.. data.States!, new StateSaveData { Id = "island", Name = "q2", IsAccepting = true }];
        data.Transitions = data.Transitions!.Where(t => !(t.FromId == "q1" && t.Symbol == "1")).ToArray();

        Assert.True(DfaValidator.TryBuild(data, out var dfa, out _));
        Assert.Equal(["island"], dfa!.UnreachableStates());
        Assert.False(dfa.IsComplete);
    }

    [Theory]
    [InlineData("0,1", new[] { "0", "1" })]
    [InlineData(" a , b ,", new[] { "a", "b" })]
    [InlineData("", new string[0])]
    [InlineData(null, new string[0])]
    public void Splits_symbol_labels(string? label, string[] expected)
        => Assert.Equal(expected, DfaValidator.SplitSymbols(label));
}

public class DfaRunnerTests
{
    private static readonly Dfa EvenZeros = Build(Fixtures.EvenZeros());

    [Theory]
    [InlineData("", true)]
    [InlineData("1", true)]
    [InlineData("0", false)]
    [InlineData("00", true)]
    [InlineData("0101", true)]
    [InlineData("0001", false)]
    [InlineData("1100100", true)]
    [InlineData("110010", false)]
    public void Accepts_words_with_an_even_number_of_zeros(string word, bool expected)
        => Assert.Equal(expected, DfaRunner.Accepts(EvenZeros, word));

    [Fact]
    public void Run_returns_the_full_trace()
    {
        var result = DfaRunner.Run(EvenZeros, "010");

        Assert.Equal(RunOutcome.Accepted, result.Outcome); // two zeros
        Assert.Equal("q0", result.FinalState);
        Assert.Equal(3, result.ConsumedSymbols);
        Assert.Equal(
            [new RunStep(0, "q0", '0', "q1"), new RunStep(1, "q1", '1', "q1"), new RunStep(2, "q1", '0', "q0")],
            result.Steps);
        Assert.Equal("q0", result.Steps[^1].ToState);
    }

    [Fact]
    public void Run_flags_symbols_outside_the_alphabet()
    {
        var result = DfaRunner.Run(EvenZeros, "01x");

        Assert.Equal(RunOutcome.InvalidSymbol, result.Outcome);
        Assert.Empty(result.Steps);
        Assert.False(DfaRunner.IsWordInAlphabet(EvenZeros, "2"));
    }

    [Fact]
    public void Partial_dfa_gets_stuck_and_reports_where()
    {
        var data = Fixtures.EvenZeros();
        data.Transitions = data.Transitions!.Where(t => !(t.FromId == "q1" && t.Symbol == "1")).ToArray();
        var dfa = Build(data);

        var result = DfaRunner.Run(dfa, "011");

        Assert.Equal(RunOutcome.Stuck, result.Outcome);
        Assert.Equal("q1", result.FinalState);
        Assert.Equal(1, result.ConsumedSymbols);
        Assert.Null(DfaRunner.Step(dfa, "q1", 1, "011"));
    }

    [Fact]
    public void Step_is_the_building_block_of_the_run()
    {
        var state = EvenZeros.InitialState;
        const string word = "0011";
        for (var i = 0; i < word.Length; i++)
        {
            var step = DfaRunner.Step(EvenZeros, state, i, word);
            Assert.NotNull(step);
            state = step!.ToState;
        }

        Assert.Equal("q0", state);
        Assert.Null(DfaRunner.Step(EvenZeros, state, word.Length, word));
    }

    private static Dfa Build(DfaSaveData data)
    {
        Assert.True(DfaValidator.TryBuild(data, out var dfa, out var problems), string.Join("; ", problems.Select(p => p.Message)));
        return dfa!;
    }
}

public class DfaJsonTests
{
    [Fact]
    public void Round_trips_through_json()
    {
        var json = DfaJson.Serialize(Fixtures.EvenZeros());
        var back = DfaJson.TryDeserialize(json, out var error);

        Assert.Null(error);
        Assert.NotNull(back);
        Assert.Equal(2, back!.States!.Length);
        Assert.Equal("0", back.Transitions![0].Symbol);
        Assert.True(DfaValidator.TryBuild(back, out _, out _));
    }

    [Theory]
    [InlineData("{")]
    [InlineData("[1,2,3]")]
    [InlineData("\"text\"")]
    public void Reports_malformed_json_instead_of_throwing(string json)
    {
        var data = DfaJson.TryDeserialize(json, out var error);
        Assert.Null(data);
        Assert.NotNull(error);
    }

    [Fact]
    public void Shipped_samples_are_valid_dfas()
    {
        var samples = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Samples"), "*.json");
        Assert.NotEmpty(samples);
        foreach (var path in samples)
        {
            var data = DfaJson.TryDeserialize(File.ReadAllText(path), out var error);
            Assert.True(data is not null, $"{Path.GetFileName(path)}: {error}");
            Assert.True(DfaValidator.TryBuild(data!, out _, out var problems), $"{Path.GetFileName(path)}: {string.Join("; ", problems.Select(p => p.Message))}");
        }
    }
}

internal static class Fixtures
{
    /// <summary>Two states; accepts binary words with an even number of zeros.</summary>
    public static DfaSaveData EvenZeros() => new()
    {
        States =
        [
            new StateSaveData { Id = "q0", Name = "q0", X = 100, Y = 200, IsInitial = true, IsAccepting = true },
            new StateSaveData { Id = "q1", Name = "q1", X = 300, Y = 200 },
        ],
        Transitions =
        [
            new TransitionSaveData { FromId = "q0", ToId = "q1", Symbol = "0" },
            new TransitionSaveData { FromId = "q0", ToId = "q0", Symbol = "1" },
            new TransitionSaveData { FromId = "q1", ToId = "q0", Symbol = "0" },
            new TransitionSaveData { FromId = "q1", ToId = "q1", Symbol = "1" },
        ],
    };
}
