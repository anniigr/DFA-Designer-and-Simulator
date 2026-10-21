namespace AutomatonEditor.Core;

public enum RunOutcome
{
    /// <summary>All symbols consumed and the final state is accepting.</summary>
    Accepted,

    /// <summary>All symbols consumed and the final state is not accepting.</summary>
    Rejected,

    /// <summary>A symbol had no transition from the current state (partial DFA); the run stopped early.</summary>
    Stuck,

    /// <summary>The word contains a symbol outside Σ.</summary>
    InvalidSymbol,
}

public sealed record RunStep(int Index, string FromState, char Symbol, string ToState);

public sealed record RunResult(RunOutcome Outcome, string FinalState, IReadOnlyList<RunStep> Steps, int ConsumedSymbols)
{
    public bool IsAccepted => Outcome == RunOutcome.Accepted;
}

/// <summary>
/// Executes a DFA on an input word. <see cref="Run"/> returns the whole trace;
/// <see cref="Step"/> computes one transition, which is what the step-by-step
/// and animated modes of the editor replay.
/// </summary>
public static class DfaRunner
{
    public static bool IsWordInAlphabet(Dfa dfa, string word)
    {
        var alphabet = new HashSet<char>(dfa.Alphabet);
        return word.All(alphabet.Contains);
    }

    public static RunStep? Step(Dfa dfa, string state, int index, string word)
    {
        if (index < 0 || index >= word.Length) return null;
        var symbol = word[index];
        var next = dfa.Next(state, symbol);
        return next is null ? null : new RunStep(index, state, symbol, next);
    }

    public static RunResult Run(Dfa dfa, string word)
    {
        if (!IsWordInAlphabet(dfa, word))
        {
            return new RunResult(RunOutcome.InvalidSymbol, dfa.InitialState, [], 0);
        }

        var steps = new List<RunStep>(word.Length);
        var state = dfa.InitialState;

        for (var i = 0; i < word.Length; i++)
        {
            var step = Step(dfa, state, i, word);
            if (step is null)
            {
                return new RunResult(RunOutcome.Stuck, state, steps, i);
            }

            steps.Add(step);
            state = step.ToState;
        }

        var outcome = dfa.IsAccepting(state) ? RunOutcome.Accepted : RunOutcome.Rejected;
        return new RunResult(outcome, state, steps, word.Length);
    }

    public static bool Accepts(Dfa dfa, string word) => Run(dfa, word).IsAccepted;
}
