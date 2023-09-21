using Microsoft.Extensions.DependencyInjection;
using SmartCmdArgs.Logic;
using SmartCmdArgs.Services;
using SmartCmdArgs.ViewModel;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartCmdArgs.Services
{
    public interface IToolWindowHistory
    {
        void Clear();
        void DeleteNewest();
        void Pause();
        void RestoreLastState();
        void RestorePrevState();
        void Resume();
        void SaveState();
        void SaveStateAndPause();
    }

    internal class ToolWindowHistory : IToolWindowHistory
    {
        private readonly Lazy<ILifeCycleService> lifeCycleService;
        private readonly Lazy<ToolWindowViewModel> toolWindowViewModel;
        private readonly Lazy<SettingsViewModel> settingsViewModel;

        private HistoryRingBuffer<SuoDataJson> _buffer;
        private int _pauseCounter = 0;

        public ToolWindowHistory(
            Lazy<ILifeCycleService> lifeCycleService,
            Lazy<ToolWindowViewModel> toolWindowViewModel,
            Lazy<SettingsViewModel> settingsViewModel)
        {
            this.lifeCycleService = lifeCycleService;
            this.toolWindowViewModel = toolWindowViewModel;
            this.settingsViewModel = settingsViewModel;

            _buffer = new HistoryRingBuffer<SuoDataJson>(500);
        }

        public void Clear()
        {
            _buffer.Clear();
        }

        public void DeleteNewest()
        {
            if (!lifeCycleService.Value.IsEnabled)
                return;

            if (_pauseCounter == 0)
                _buffer.PopFront();
        }

        public void SaveState()
        {
            if (!lifeCycleService.Value.IsEnabled)
                return;

            if (_pauseCounter == 0)
                _buffer.Push(SuoDataSerializer.Serialize(toolWindowViewModel.Value, settingsViewModel.Value));
        }

        public void SaveStateAndPause()
        {
            SaveState();
            Pause();
        }

        public void Pause()
        {
            _pauseCounter++;
        }

        public void Resume()
        {
            if (_pauseCounter > 0)
                _pauseCounter--;
        }

        private void RestoreCurrentState()
        {
            if (!lifeCycleService.Value.IsEnabled)
                return;

            if (_buffer.TryGetCurrent(out SuoDataJson data))
            {
                toolWindowViewModel.Value.TreeViewModel.ShowAllProjects = data.ShowAllProjects;

                foreach (var pair in data.ProjectArguments)
                {
                    toolWindowViewModel.Value.PopulateFromProjectData(pair.Key, pair.Value);
                }
            }
        }

        public void RestoreLastState()
        {
            if (!lifeCycleService.Value.IsEnabled)
                return;

            if (_buffer.IsCurrentFront)
                SaveState();

            if (_buffer.MoveBack())
            {
                RestoreCurrentState();
            }
        }

        public void RestorePrevState()
        {
            if (!lifeCycleService.Value.IsEnabled)
                return;

            if (_buffer.MoveForward())
            {
                RestoreCurrentState();

                if (_buffer.IsCurrentFront)
                    _buffer.PopFront();
            }
        }
    }

    class HistoryRingBuffer<T> : IEnumerable<T>
    {
        private readonly T[] _items;

        private int _front;     // index of the item at the front
        private int _current;

        public int Capacaty => _items.Length;

        public int Count { private set; get; }

        public bool IsEmpty => Count == 0;

        public bool IsCurrentFront => _front == _current;

        public T Current => _items[_current];

        public HistoryRingBuffer(int size)
        {
            _front = -1;
            _current = -1;
            Count = 0;

            if (size < 1)
                throw new ArgumentOutOfRangeException("Size of HistoryRingBuffer must be greater than 0.");

            _items = new T[size];
        }

        private void IncrementCount(int value = 1)
        {
            Count = Math.Min(Capacaty, Count + value);
        }

        private int IncIdx(int idx, int count = 1)
        {
            return (idx + count) % Capacaty;
        }

        private int DecIdx(int idx, int count = 1)
        {
            return (idx - count) % Capacaty;
        }

        private int GetInclusiveIntervalSize(int start, int end)
        {
            if (end < start)
                return end - start + Capacaty + 1;
            else
                return end - start + 1;
        }

        public void Clear()
        {
            _front = -1;
            _current = -1;
            Count = 0;

            Array.Clear(_items, 0, _items.Length);
        }

        public void PopFront()
        {
            if (Count > 1)
            {
                var newFront = DecIdx(_front);
                if (IsCurrentFront)
                    _current = newFront;
                _front = newFront;

                Count--;
            }
            else
            {
                Clear();
            }
        }

        public void Push(T item)
        {
            if (!IsCurrentFront)
                Count = Count - GetInclusiveIntervalSize(_current, _front);
            else
                _current = IncIdx(_current);

            _front = _current;
            _items[_front] = item;
            IncrementCount();
        }

        public bool TryGetCurrent(out T item)
        {
            if (IsEmpty)
            {
                item = default(T);
                return false;
            }
            else
            {
                item = Current;
                return true;
            }
        }

        public bool MoveBack()
        {
            if (GetInclusiveIntervalSize(_current, _front) == Count)
                return false;

            _current = DecIdx(_current);
            return true;
        }

        public bool MoveForward()
        {
            if (IsCurrentFront)
                return false;

            _current = IncIdx(_current);
            return true;
        }

        public IEnumerator<T> GetEnumerator()
        {
            for (int i = 0, idx = _front; i < Count; i++, idx = IncIdx(idx))
            {
                yield return _items[idx];
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
