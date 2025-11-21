

using Cysharp.Threading.Tasks;

namespace AsyncStateMachine {

    public interface IState<TState> {
        UniTask OnEnter(TState from);
        UniTask OnExit(TState to);
    }

    public interface ISyncState<TState> {
        void OnEnter(TState from);
        void OnExit(TState to);
    }

    public class State<TState> : IState<TState> {
        public TState ID;

        protected OnAsyncEnterCallback<TState> OnEnterCB;
        protected OnAsyncExitCallback<TState> OnExitCB;

        public State(TState id, OnAsyncEnterCallback<TState> onEnter, OnAsyncExitCallback<TState> onExit) {
            ID = id;
            OnEnterCB = onEnter;
            OnExitCB = onExit;
        }

        public UniTask OnEnter(TState from) {
            return OnEnterCB(from);
        }

        public UniTask OnExit(TState to) {
            return OnExitCB(to);
        }
    }

}
