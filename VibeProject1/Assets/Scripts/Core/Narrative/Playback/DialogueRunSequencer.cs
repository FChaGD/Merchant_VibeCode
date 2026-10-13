using System;

namespace Game.Core
{
    /// <summary>
    /// 대본 실행 순서 조율(설계 87 §7.1, 검진 후 보완). Yarn은 중단(Stop) 뒤 완료 신호를 비동기로 늦게 보낼 수 있고, 완료 신호 처리 뒤에도 내부 정리를
    /// 계속한다. 그 사이 새 대본을 시작하면 늦은 신호가 새 대본의 "끝까지 진행"으로 잡히거나 Yarn 정리가 새 대본 상태를 지운다. 그래서 엔진 완료 신호 →
    /// 다음 프레임(정리 끝) 사이에 들어온 시작 요청은 보류했다가 그때 시작한다. Yarn을 모르는 순수 C#이라 순서 규칙을 EditMode에서 검증한다.
    /// 전제: 실행 중인 대본을 엔진에서 중단하면 엔진은 반드시 완료 신호를 한 번 보낸다(Yarn DialogueRunner.Stop).
    /// </summary>
    public sealed class DialogueRunSequencer
    {
        private enum State { Idle, Running, Stopping, Settling }

        private State state = State.Idle;
        private Action<bool> onFinished;
        private Action pendingStart;
        private Action<bool> pendingFinished;

        /// <summary>실행 중이거나, 엔진 정리를 기다리거나, 시작 보류 중이면 true.</summary>
        public bool IsBusy => state != State.Idle || pendingStart != null;

        /// <summary>false = 거부(이미 실행 중이거나 보류 중인 대본이 있음). 엔진 정리 중이면 보류했다가 정리가 끝나면 시작한다.</summary>
        public bool TryBegin(Action start, Action<bool> onFinished)
        {
            if (pendingStart != null) return false;
            switch (state)
            {
                case State.Idle:
                    Launch(start, onFinished);
                    return true;
                case State.Stopping:
                case State.Settling:
                    pendingStart = start;
                    pendingFinished = onFinished;
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>즉시 종료. true = 엔진에서 실행 중인 대본이 있어 엔진 중단이 필요하다. 보류 중인 대본은 시작하지 않고 중단으로 끝낸다.</summary>
        public bool Stop()
        {
            if (pendingStart != null)
            {
                var callback = pendingFinished;
                pendingStart = null;
                pendingFinished = null;
                callback?.Invoke(false);
                return false;
            }
            if (state != State.Running) return false;
            state = State.Stopping;
            Finish(false);
            return true;
        }

        /// <summary>엔진의 대본 완료 신호. 실행 중이었으면 끝까지 진행, 중단 뒤 늦게 온 신호면 무시한다. 어느 쪽이든 다음 프레임 정리 대기로 넘어간다.</summary>
        public void NotifyEngineComplete()
        {
            switch (state)
            {
                case State.Running:
                    state = State.Settling;
                    Finish(true);
                    break;
                case State.Stopping:
                    state = State.Settling;
                    break;
            }
        }

        /// <summary>엔진 정리 완료(완료 신호 다음 프레임). 보류된 대본이 있으면 이때 시작한다.</summary>
        public void NotifySettled()
        {
            if (state != State.Settling) return;
            state = State.Idle;
            if (pendingStart == null) return;
            var start = pendingStart;
            var callback = pendingFinished;
            pendingStart = null;
            pendingFinished = null;
            Launch(start, callback);
        }

        private void Launch(Action start, Action<bool> callback)
        {
            state = State.Running;
            onFinished = callback;
            start();
        }

        // 상태를 먼저 바꾼 뒤 부른다 - 콜백 안의 새 시작 요청이 보류로 처리되게.
        private void Finish(bool completed)
        {
            var callback = onFinished;
            onFinished = null;
            callback?.Invoke(completed);
        }
    }
}
