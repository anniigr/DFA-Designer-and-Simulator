namespace AutomatonEditor.Core;

public enum DfaProblemKind
{
    NoStates,
    MissingStateId,
    MissingStateName,
    DuplicateStateId,
    DuplicateStateName,
    NoInitialState,
    MultipleInitialStates,
    MissingTransitionEndpoint,
    UnknownState,
    EmptySymbol,
    SymbolNotSingleCharacter,
    NonDeterministic,
}

public sealed record DfaProblem(DfaProblemKind Kind, string Message);

/// <summary>
/// Checks a <see cref="DfaSaveData"/> document against the definition of a DFA
/// and builds an immutable <see cref="Dfa"/> from it. Every rule the editor
/// enforces interactively is re-checked here, so a hand-edited or corrupted
/// file cannot put the simulator into a state the editor could not produce.
/// </summary>
public static class DfaValidator
{
    public static IReadOnlyList<DfaProblem> Validate(DfaSaveData data)
    {
        TryBuild(data, out var problems);
        return problems;
    }

    public static bool TryBuild(DfaSaveData data, out IReadOnlyList<DfaProblem> problems)
        => TryBuild(data, out _, out problems);

    public static bool TryBuild(DfaSaveData data, out Dfa? dfa, out IReadOnlyList<DfaProblem> problems)
    {
        var list = new List<DfaProblem>();
        dfa = null;

        var states = data.States ?? [];
        var transitions = data.Transitions ?? [];

        if (states.Length == 0)
        {
            list.Add(new(DfaProblemKind.NoStates, "The automaton has no states."));
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var names = new HashSet<string>(StringComparer.Ordinal);
        var initial = new List<string>();
        var accepting = new HashSet<string>(StringComparer.Ordinal);
        var order = new List<string>();

        for (var i = 0; i < states.Length; i++)
        {
            var s = states[i];
            if (string.IsNullOrWhiteSpace(s.Id))
            {
                list.Add(new(DfaProblemKind.MissingStateId, $"State #{i + 1} has no Id."));
                continue;
            }

            if (string.IsNullOrWhiteSpace(s.Name))
            {
                list.Add(new(DfaProblemKind.MissingStateName, $"State '{s.Id}' has no Name."));
            }
            else if (!names.Add(s.Name))
            {
                list.Add(new(DfaProblemKind.DuplicateStateName, $"State name '{s.Name}' is used more than once."));
            }

            if (!ids.Add(s.Id))
            {
                list.Add(new(DfaProblemKind.DuplicateStateId, $"State id '{s.Id}' is used more than once."));
                continue;
            }

            order.Add(s.Id);
            if (s.IsInitial) initial.Add(s.Id);
            if (s.IsAccepting) accepting.Add(s.Id);
        }

        if (states.Length > 0 && initial.Count == 0)
        {
            list.Add(new(DfaProblemKind.NoInitialState, "No state is marked as initial."));
        }
        else if (initial.Count > 1)
        {
            list.Add(new(DfaProblemKind.MultipleInitialStates, $"A DFA has exactly one initial state; found {initial.Count}."));
        }

        var delta = new Dictionary<(string, char), string>();

        for (var i = 0; i < transitions.Length; i++)
        {
            var t = transitions[i];
            if (string.IsNullOrWhiteSpace(t.FromId) || string.IsNullOrWhiteSpace(t.ToId))
            {
                list.Add(new(DfaProblemKind.MissingTransitionEndpoint, $"Transition #{i + 1} is missing FromId or ToId."));
                continue;
            }

            if (!ids.Contains(t.FromId) || !ids.Contains(t.ToId))
            {
                list.Add(new(DfaProblemKind.UnknownState, $"Transition #{i + 1} references a state that does not exist (From: {t.FromId}, To: {t.ToId})."));
                continue;
            }

            var symbols = SplitSymbols(t.Symbol);
            if (symbols.Count == 0)
            {
                list.Add(new(DfaProblemKind.EmptySymbol, $"Transition #{i + 1} ({t.FromId} → {t.ToId}) has no symbol; ε-transitions are not allowed in a DFA."));
                continue;
            }

            foreach (var symbol in symbols)
            {
                if (symbol.Length != 1)
                {
                    list.Add(new(DfaProblemKind.SymbolNotSingleCharacter, $"Symbol '{symbol}' on transition #{i + 1} must be a single character."));
                    continue;
                }

                var key = (t.FromId, symbol[0]);
                if (delta.TryGetValue(key, out var existing))
                {
                    list.Add(new(DfaProblemKind.NonDeterministic,
                        $"State '{t.FromId}' has two transitions on '{symbol}' (to '{existing}' and '{t.ToId}'); a DFA allows one."));
                    continue;
                }

                delta[key] = t.ToId;
            }
        }

        problems = list;
        if (list.Count > 0)
        {
            return false;
        }

        dfa = new Dfa(order, initial[0], accepting, delta);
        return true;
    }

    /// <summary>"0, 1" → ["0", "1"]; empty pieces are dropped.</summary>
    public static IReadOnlyList<string> SplitSymbols(string? label)
        => string.IsNullOrWhiteSpace(label)
            ? []
            : label.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
}
