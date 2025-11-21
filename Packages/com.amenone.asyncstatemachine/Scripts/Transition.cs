namespace AsyncStateMachine {
    public enum TransitionPhase {
        NOT_STARTED,
        EXITING_FROM,
        EXITED_FROM,
        ENTERING_TO,
        ENTERED_TO,
        FINISHED
    }

    public class Transition<TState> {
        public TState From;
        public TState To;
        public TransitionPhase Phase = TransitionPhase.NOT_STARTED;

        public Transition(TState from, TState to) {
            From = from;
            To = to;
        }
    }

}
