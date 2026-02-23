using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace FlowWheel.Core
{
    public enum ScrollState
    {
        Idle,
        Dragging,
        InertialScrolling,
        ReadingMode
    }

    public class ScrollEngine
    {
        private volatile bool _isRunning = false;
        private readonly object _lock = new object();
        private double _accumulatedDelta = 0;
        private int _runId = 0;
        private double _filteredSpeedV = 0;
        private double _filteredSpeedH = 0;

        public ScrollState CurrentState { get; private set; } = ScrollState.Idle;
        
        // Settings
        public float Sensitivity { get; set; } = 0.8f; // Speed multiplier
        public int Deadzone { get; set; } = 20; // Pixels
        public int TickRate { get; set; } = 120; // Updates per second
        public int MinStep { get; set; } = 1; // Minimum delta to send (fix for Explorer/Win32)
        public double Friction { get; set; } = 5.0; // Friction factor
        public double ResponseTime { get; set; } = 0.04;
        public double AxisLockRatio { get; set; } = 1.8;
        public int SoftStartRange { get; set; } = 12;

        // Current State
        private double _currentSpeed = 0; // Delta per second
        private double _currentHSpeed = 0; // Horizontal Speed
        private double _accumulatedHDelta = 0; // Horizontal Accumulator
        private NativeMethods.POINT _origin;
        private NativeMethods.POINT _current;
        private NativeMethods.POINT _lastPos; // For calculating throw velocity
        private long _lastPosTime; // Timestamp for velocity calculation

        // Inertia
        private double _inertiaSpeedV = 0;
        private double _inertiaSpeedH = 0;

        private readonly SyncScrollManager _syncManager = new SyncScrollManager();
        public bool IsSyncEnabled { get; set; } = false;

        // Reading Mode
        private double _readingSpeed = 0; // Pixels per second

        public ScrollEngine()
        {
        }

        public void StartReadingMode(double initialSpeed)
        {
            int runId;
            lock (_lock)
            {
                CurrentState = ScrollState.ReadingMode;
                _readingSpeed = initialSpeed;
                _currentSpeed = -initialSpeed; // Negative is down (usually)
                _currentHSpeed = 0;
                _accumulatedDelta = 0;
                _accumulatedHDelta = 0;
                _filteredSpeedV = _currentSpeed;
                _filteredSpeedH = 0;
                _runId++;
                runId = _runId;
                
                _isRunning = true;
                Task.Run(() => Loop(runId));
            }
        }

        public void AdjustReadingSpeed(double delta)
        {
            lock (_lock)
            {
                if (CurrentState != ScrollState.ReadingMode) return;
                _readingSpeed += delta;
                if (_readingSpeed < 0) _readingSpeed = 0; // Don't reverse, just stop
                if (_readingSpeed > 1000) _readingSpeed = 1000;
                _currentSpeed = -_readingSpeed;
            }
        }

        public void StartDrag(NativeMethods.POINT origin)
        {
            int runId;
            lock (_lock)
            {
                if (_isRunning && CurrentState == ScrollState.ReadingMode) return;

                CurrentState = ScrollState.Dragging;
                _isRunning = true;
                _origin = origin;
                _current = origin;
                _lastPos = origin;
                _lastPosTime = Stopwatch.GetTimestamp();
                
                _currentSpeed = 0;
                _accumulatedDelta = 0;
                _currentHSpeed = 0;
                _accumulatedHDelta = 0;
                _filteredSpeedV = 0;
                _filteredSpeedH = 0;
                _runId++;
                runId = _runId;

                if (IsSyncEnabled)
                {
                    _syncManager.UpdateTargets(origin);
                }

                Task.Run(() => Loop(runId));
            }
        }

        public void ReleaseDrag()
        {
            lock (_lock)
            {
                if (CurrentState != ScrollState.Dragging) return;

                // Calculate release velocity
                long now = Stopwatch.GetTimestamp();
                double dt = (now - _lastPosTime) / (double)Stopwatch.Frequency;
                
                if (dt > 0.1) // If held still for too long, no inertia
                {
                    _currentSpeed = 0;
                    _currentHSpeed = 0;
                }

                // Trigger inertia if speed is significant
                if (Math.Abs(_currentSpeed) > 100 || Math.Abs(_currentHSpeed) > 100)
                {
                    CurrentState = ScrollState.InertialScrolling;
                    _inertiaSpeedV = _currentSpeed;
                    _inertiaSpeedH = _currentHSpeed;
                }
                else
                {
                    Stop();
                }
            }
        }

        public void Stop()
        {
            lock (_lock)
            {
                CurrentState = ScrollState.Idle;
                _isRunning = false;
                _runId++;
                _accumulatedDelta = 0;
                _accumulatedHDelta = 0;
                _filteredSpeedV = 0;
                _filteredSpeedH = 0;
            }
        }

        public void UpdateDragPosition(NativeMethods.POINT pt)
        {
            if (CurrentState != ScrollState.Dragging) return;

            long now = Stopwatch.GetTimestamp();
            _lastPos = _current;
            _lastPosTime = now;
            _current = pt;
            
            CalculateSpeed();
        }
        
        // Legacy support for click-toggle mode
        public void Start(NativeMethods.POINT origin)
        {
            StartDrag(origin);
        }

        public void UpdatePosition(NativeMethods.POINT pt)
        {
            UpdateDragPosition(pt);
        }
        // End legacy support

        private void CalculateSpeed()
        {
            // Vertical
            int dy = _current.y - _origin.y;
            int distY = Math.Abs(dy);

            // ... (Calculation logic remains similar but simplified for 1:1 feel if needed)
            // For now, keep the deadzone/sensitivity logic as it maps distance to speed well for "Joystick" style
            // For "Grab & Throw" style (iPhone style), we actually need distance to map to position delta, not speed.
            // But FlowWheel's core identity is "Joystick" style (middle click auto scroll).
            // So "Grab" here means "Grab the Joystick Handle", not "Grab the Page".
            // The user requested "Grab & Throw", which implies inertia. 
            // So we keep the Joystick logic (Distance = Speed), but add Inertia on Release.

            if (distY < Deadzone)
            {
                _currentSpeed = 0;
            }
            else
            {
                double effective = distY - Deadzone;
                double rawSpeed = effective * Sensitivity;
                if (SoftStartRange > 0 && effective < SoftStartRange)
                {
                    double t = effective / SoftStartRange;
                    t = t * t * (3.0 - 2.0 * t);
                    rawSpeed *= t;
                }
                if (rawSpeed > 5000) rawSpeed = 5000;

                if (dy > 0) // Mouse Down -> Scroll Down (Negative)
                {
                    _currentSpeed = -rawSpeed;
                }
                else // Mouse Up -> Scroll Up (Positive)
                {
                    _currentSpeed = rawSpeed;
                }
            }

            // Horizontal
            int dx = _current.x - _origin.x;
            int distX = Math.Abs(dx);

            if (distX < Deadzone)
            {
                _currentHSpeed = 0;
            }
            else
            {
                double effective = distX - Deadzone;
                double rawHSpeed = effective * Sensitivity;
                if (SoftStartRange > 0 && effective < SoftStartRange)
                {
                    double t = effective / SoftStartRange;
                    t = t * t * (3.0 - 2.0 * t);
                    rawHSpeed *= t;
                }
                if (rawHSpeed > 5000) rawHSpeed = 5000;

                if (dx > 0) // Mouse Right -> Scroll Right
                {
                    _currentHSpeed = rawHSpeed; 
                }
                else // Mouse Left -> Scroll Left
                {
                    _currentHSpeed = -rawHSpeed;
                }
            }

            if (AxisLockRatio > 1.0 && distX >= Deadzone && distY >= Deadzone)
            {
                if (distY > distX * AxisLockRatio)
                {
                    _currentHSpeed = 0;
                }
                else if (distX > distY * AxisLockRatio)
                {
                    _currentSpeed = 0;
                }
            }
        }

        private async Task Loop(int runId)
        {
            long lastTick = Stopwatch.GetTimestamp();
            long intervalTicks = (long)(Stopwatch.Frequency / (double)TickRate);
            if (intervalTicks < 1) intervalTicks = 1;
            long nextTick = lastTick;

            while (true)
            {
                if (!_isRunning || runId != Volatile.Read(ref _runId)) break;

                long currentTick = Stopwatch.GetTimestamp();
                double dt = (currentTick - lastTick) / (double)Stopwatch.Frequency;
                lastTick = currentTick;

                double targetSpeedV = _currentSpeed;
                double targetSpeedH = _currentHSpeed;

                // Handle Inertia
                if (CurrentState == ScrollState.InertialScrolling)
                {
                    // Apply friction
                    double frictionFactor = Math.Exp(-Friction * dt);
                    _inertiaSpeedV *= frictionFactor;
                    _inertiaSpeedH *= frictionFactor;

                    targetSpeedV = _inertiaSpeedV;
                    targetSpeedH = _inertiaSpeedH;
                    _filteredSpeedV = targetSpeedV;
                    _filteredSpeedH = targetSpeedH;

                    // Stop if too slow
                    if (Math.Abs(_inertiaSpeedV) < 10 && Math.Abs(_inertiaSpeedH) < 10)
                    {
                        lock (_lock)
                        {
                            Stop();
                        }
                    }
                }
                else
                {
                    if (ResponseTime > 0.0001 && dt > 0)
                    {
                        double alpha = 1.0 - Math.Exp(-dt / ResponseTime);
                        _filteredSpeedV += (targetSpeedV - _filteredSpeedV) * alpha;
                        _filteredSpeedH += (targetSpeedH - _filteredSpeedH) * alpha;
                        targetSpeedV = _filteredSpeedV;
                        targetSpeedH = _filteredSpeedH;
                    }
                    else
                    {
                        _filteredSpeedV = targetSpeedV;
                        _filteredSpeedH = targetSpeedH;
                    }
                }

                if (Math.Abs(targetSpeedV) > 0.1)
                {
                    // Add delta for this frame
                    _accumulatedDelta += targetSpeedV * dt;

                    int steps = 0;
                    // Compatibility Fix: Some apps (Explorer) ignore small deltas (e.g. < 10 or 30).
                    // We accumulate until we reach MinStep.
                    if (Math.Abs(_accumulatedDelta) >= MinStep)
                    {
                        steps = (int)_accumulatedDelta;
                        _accumulatedDelta -= steps;
                    }

                    if (steps != 0)
                    {
                        SendScrollEvent(steps, false);
                    }
                }
                else
                {
                    _accumulatedDelta = 0;
                }

                // Horizontal Processing
                if (Math.Abs(targetSpeedH) > 0.1)
                {
                    _accumulatedHDelta += targetSpeedH * dt;
                    int hSteps = 0;
                    if (Math.Abs(_accumulatedHDelta) >= MinStep)
                    {
                        hSteps = (int)_accumulatedHDelta;
                        _accumulatedHDelta -= hSteps;
                    }

                    if (hSteps != 0)
                    {
                        SendScrollEvent(hSteps, true);
                    }
                }
                else
                {
                    _accumulatedHDelta = 0;
                }

                nextTick += intervalTicks;
                long now = Stopwatch.GetTimestamp();
                long remaining = nextTick - now;
                if (remaining > 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(remaining / (double)Stopwatch.Frequency)).ConfigureAwait(false);
                }
                else if (-remaining > intervalTicks * 5)
                {
                    nextTick = now;
                }
            }
        }

        private void SendScrollEvent(int delta, bool isHorizontal)
        {
            NativeMethods.INPUT[] inputs = new NativeMethods.INPUT[1];
            inputs[0].type = NativeMethods.INPUT_MOUSE;
            inputs[0].mi = new NativeMethods.MOUSEINPUT
            {
                dx = 0,
                dy = 0,
                mouseData = (uint)delta, // Cast acts as signed short representation
                dwFlags = isHorizontal ? (uint)NativeMethods.MOUSEEVENTF_HWHEEL : (uint)NativeMethods.MOUSEEVENTF_WHEEL,
                time = 0,
                dwExtraInfo = MouseHook.INJECTED_SIGNATURE
            };

            NativeMethods.SendInput(1, inputs, Marshal.SizeOf(typeof(NativeMethods.INPUT)));

            if (IsSyncEnabled)
            {
                _syncManager.Scroll(delta, isHorizontal);
            }
        }
    }
}
