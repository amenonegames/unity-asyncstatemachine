using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace AsyncStateMachine {

    public delegate void StateChange<TState>(Transition<TState> transition);
    public delegate UniTask OnAsyncEnterCallback<TState>(TState from);
    public delegate UniTask OnAsyncExitCallback<TState>(TState to);
    public delegate void OnSyncEnterCallback<TState>(TState from);
    public delegate void OnSyncExitCallback<TState>(TState to);

    public delegate void DebugMessage(string msg);

    public enum DEBUG_MODE {
        NONE,
        UNITY_DEBUG_LOG,
        EVENT
    }

    public class EventTransitionMap<TState, TEvent> {
        private Dictionary<TState, Dictionary<TEvent, TState>> transitions = new Dictionary<TState, Dictionary<TEvent, TState>>();

        public void Register(TState fromState, TEvent onEvent, TState toState) {
            if (!transitions.ContainsKey(fromState)) {
                transitions[fromState] = new Dictionary<TEvent, TState>();
            }
            transitions[fromState][onEvent] = toState;
        }

        public bool TryGetDestination(TState fromState, TEvent onEvent, out TState toState) {
            toState = default;
            if (!transitions.ContainsKey(fromState)) {
                return false;
            }
            return transitions[fromState].TryGetValue(onEvent, out toState);
        }

        public void RemoveTransitionsFrom(TState fromState) {
            transitions.Remove(fromState);
        }

        public void Clear() {
            transitions.Clear();
        }
    }

    public class Statemachine<TState, TEvent> {

        public DEBUG_MODE DebugMode = DEBUG_MODE.UNITY_DEBUG_LOG;

        public TState CurrentState { get; private set; }
        public Transition<TState> CurrentTransition { get; private set; }

        protected bool firstTransition = true;

        protected Dictionary<TState, IState<TState>> states = new Dictionary<TState, IState<TState>>();
        protected EventTransitionMap<TState, TEvent> eventTransitions = new EventTransitionMap<TState, TEvent>();

        public event StateChange<TState> OnStateEntering;
        public event StateChange<TState> OnStateExiting;
        public event StateChange<TState> OnStateEntered;
        public event StateChange<TState> OnStateExited;

        public event DebugMessage OnDebugMessage;

        public Statemachine() {
        }

        public async UniTask TransitionToState(TState state) {
            if (CurrentTransition != null) {
                DebugLog("Warning: Statemachine already transitioning from " + CurrentTransition.From + " to " + CurrentTransition.To);
                return;
            }

            if (!firstTransition && state.Equals(CurrentState)) {
                DebugLog("Warning: Statemachine is already in state " + state);
                return;
            }

            CurrentTransition = new Transition<TState>(CurrentState, state);

            if (!firstTransition) {
                CurrentTransition.Phase = TransitionPhase.EXITING_FROM;
                OnStateExiting?.Invoke(CurrentTransition);
                await states[CurrentTransition.From].OnExit(CurrentTransition.To);
                CurrentTransition.Phase = TransitionPhase.EXITED_FROM;
                OnStateExited?.Invoke(CurrentTransition);
            }

            CurrentState = CurrentTransition.To;
            CurrentTransition.Phase = TransitionPhase.ENTERING_TO;
            OnStateEntering?.Invoke(CurrentTransition);
            await states[CurrentTransition.To].OnEnter(CurrentTransition.From);
            CurrentTransition.Phase = TransitionPhase.ENTERED_TO;
            Transition<TState> prevTransition = CurrentTransition;
            CurrentTransition = null;
            OnStateEntered?.Invoke(prevTransition);
            firstTransition = false;
        }

        public bool CurrentStateIs(TState state, bool includeTransition = false) {
            if (CurrentTransition == null) {
                return state.Equals(CurrentState);
            } else if (includeTransition) {
                if (CurrentTransition.Phase == TransitionPhase.EXITING_FROM)
                    return state.Equals(CurrentTransition.From);
                if (CurrentTransition.Phase == TransitionPhase.ENTERING_TO)
                    return state.Equals(CurrentTransition.To);
            }
            return false;
        }

        public IState<TState> AddState(TState id, IState<TState> state) {
            states.Add(id, state);
            return state;
        }

        public IState<TState> AddState(TState id, ISyncState<TState> syncState) {
            return AddSyncState(id, syncState);
        }

        public IState<TState> AddState(TState id, OnSyncEnterCallback<TState> onEnterCB, OnSyncExitCallback<TState> onExitCB) {
            return AddStateWithCallbacks(id, onEnterCB, onExitCB);
        }

        public State<TState> AddState(TState id, OnAsyncEnterCallback<TState> onEnterCB, OnAsyncExitCallback<TState> onExitCB) {
            return AddStateWithTasks(id, onEnterCB, onExitCB);
        }

        public IState<TState> AddState(TState id, OnSyncEnterCallback<TState> onEnterCB, OnAsyncExitCallback<TState> onExitCB) {
            UniTask OnEnter(TState from) { onEnterCB(from); return UniTask.CompletedTask; }

            State<TState> state = new State<TState>(id, OnEnter, onExitCB);
            AddState(id, state);
            return state;
        }

        public State<TState> AddState(TState id, OnAsyncEnterCallback<TState> onEnterCB, OnSyncExitCallback<TState> onExitCB) {
            UniTask OnExit(TState to) { onExitCB(to); return UniTask.CompletedTask; }

            State<TState> state = new State<TState>(id, onEnterCB, OnExit);
            AddState(id, state);
            return state;
        }

        public IState<TState> AddSyncState(TState id, ISyncState<TState> syncState) {
            UniTask OnEnter(TState from) { syncState.OnEnter(from); return UniTask.CompletedTask; }
            UniTask OnExit(TState to) { syncState.OnExit(to); return UniTask.CompletedTask; }

            State<TState> state = new State<TState>(id, OnEnter, OnExit);
            AddState(id, state);
            return state;
        }

        public IState<TState> AddStateWithCallbacks(TState id, OnSyncEnterCallback<TState> onEnterCB, OnSyncExitCallback<TState> onExitCB) {
            UniTask OnEnter(TState from) { onEnterCB(from); return UniTask.CompletedTask; }
            UniTask OnExit(TState to) { onExitCB(to); return UniTask.CompletedTask; }

            State<TState> state = new State<TState>(id, OnEnter, OnExit);
            AddState(id, state);
            return state;
        }

        public State<TState> AddStateWithTasks(TState id, OnAsyncEnterCallback<TState> onEnterCB, OnAsyncExitCallback<TState> onExitCB) {
            State<TState> state = new State<TState>(id, onEnterCB, onExitCB);
            AddState(id, state);
            return state;
        }

        public void RemoveState(TState id) {
            if (!states.ContainsKey(id))
                throw new Exception("StateMachine does not contain the state " + id);

            IState<TState> state = states[id];
            states.Remove(id);
            eventTransitions.RemoveTransitionsFrom(id);
        }

        public void RemoveAllStates() {
            states.Clear();
            eventTransitions.Clear();
        }

        public void RegisterTransition(TState fromState, TEvent onEvent, TState toState) {
            if (!states.ContainsKey(fromState))
                throw new Exception("StateMachine does not contain the state " + fromState);
            if (!states.ContainsKey(toState))
                throw new Exception("StateMachine does not contain the state " + toState);

            eventTransitions.Register(fromState, onEvent, toState);
        }

        public async UniTask<bool> ProcessEvent(TEvent evt) {
            if (CurrentTransition != null) {
                DebugLog("Warning: Cannot process event during transition");
                return false;
            }

            if (firstTransition) {
                DebugLog("Warning: Cannot process event before initial state is set");
                return false;
            }

            if (!eventTransitions.TryGetDestination(CurrentState, evt, out TState nextState)) {
                return false;
            }

            await TransitionToState(nextState);
            return true;
        }

        public bool IsInTransition() {
            return CurrentTransition != null;
        }

        protected void DebugLog(string msg) {
            switch (DebugMode) {
                case DEBUG_MODE.UNITY_DEBUG_LOG:
                    Debug.Log(msg);
                    break;
                case DEBUG_MODE.EVENT:
                    OnDebugMessage?.Invoke(msg);
                    break;

            }
        }
    }
}
