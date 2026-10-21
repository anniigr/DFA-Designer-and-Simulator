namespace AutomatonEditor.Core;

/// <summary>
/// Immutable description of a deterministic finite automaton, independent of
/// any UI: state ids are opaque strings, transitions are keyed by
/// (state, symbol) and the alphabet is derived from the transitions.
/// Build one with <see cref="DfaValidator.TryBuild"/>, which rejects anything
/// that is not a well-formed DFA.
/// </summary>
public sealed class Dfa
{
    private readonly Dictionary<(string State, char Symbol), string> _delta;

    internal Dfa(
        IReadOnlyList<string> states,
        string initialState,
        IReadOnlySet<string> acceptingStates,
        Dictionary<(string State, char Symbol), string> delta)
    {
        States = states;
        InitialState = initialState;
        AcceptingStates = acceptingStates;
        _delta = delta;
        Alphabet = delta.Keys.Select(k => k.Symbol).Distinct().OrderBy(c => c).ToArray();
    }

    public IReadOnlyList<string> States { get; }

    public string InitialState { get; }

    public IReadOnlySet<string> AcceptingStates { get; }

    /// <summary>Σ: every symbol that appears on at least one transition, sorted.</summary>
    public IReadOnlyList<char> Alphabet { get; }

    public int TransitionCount => _delta.Count;

    /// <summary>δ(state, symbol), or null when the DFA is partial and has no transition here.</summary>
    public string? Next(string state, char symbol)
        => _delta.TryGetValue((state, symbol), out var target) ? target : null;

    public bool IsAccepting(string state) => AcceptingStates.Contains(state);

    /// <summary>True when δ is defined for every (state, symbol) pair, i.e. the DFA is total.</summary>
    public bool IsComplete => States.All(s => Alphabet.All(c => Next(s, c) is not null));

    /// <summary>
    /// States that cannot be reached from the initial state. They do not affect
    /// the language but usually indicate an editing mistake.
    /// </summary>
    public IReadOnlyList<string> UnreachableStates()
    {
        var seen = new HashSet<string> { InitialState };
        var queue = new Queue<string>();
        queue.Enqueue(InitialState);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var symbol in Alphabet)
            {
                if (Next(current, symbol) is { } next && seen.Add(next))
                {
                    queue.Enqueue(next);
                }
            }
        }

        return States.Where(s => !seen.Contains(s)).ToList();
    }
}
