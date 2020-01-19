﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;

namespace FeatureFlowFramework.Helper
{
    public class RWLock
    {
        const int NO_LOCKID = 0;
        const int WRITE_LOCKID = NO_LOCKID + 1;

        /// <summary>
        /// Multiple read-locks are allowed in parallel while write-locks are always exclusive.
        /// A lockId larger than NO_LOCKID (0) implies a write-lock, while a lockId smaller than NO_LOCKID implies a read-lock.
        /// When entering a read-lock, the lockId is decreased and increased when leaving a read-lock.
        /// When entering a write-lock, a positive lockId (greater than NO_LOCK) is set and set back to NO_LOCK when the write-lock is left.
        /// </summary>
        volatile int lockId = NO_LOCKID;
        volatile int maxReadPressure = 0;
        volatile int maxWritePressure = 0;

        AsyncManualResetEvent mre = new AsyncManualResetEvent(true);

        SpinWaitBehaviour defaultSpinningBehaviour = SpinWaitBehaviour.Balanced;

        public RWLock(SpinWaitBehaviour defaultSpinningBehaviour = SpinWaitBehaviour.Balanced)
        {
            this.defaultSpinningBehaviour = defaultSpinningBehaviour;
        }

        public enum SpinWaitBehaviour
        {
            Balanced,
            NoSpinning,
            OnlySpinning
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadLock ForReading()
        {
            return ForReading(defaultSpinningBehaviour);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadLock ForReading(SpinWaitBehaviour spinWaitBehaviour)
        {
            SpinWait spinWait = new SpinWait();
            int myPressure = 0;
            var currentLockId = lockId;
            var newLockId = currentLockId - 1;            
            while (ReaderMustWait(currentLockId, myPressure) || currentLockId != Interlocked.CompareExchange(ref lockId, newLockId, currentLockId))
            {
                myPressure++;
                if (myPressure > maxReadPressure) maxReadPressure = myPressure;

                if (spinWaitBehaviour == SpinWaitBehaviour.OnlySpinning ||
                    (spinWaitBehaviour == SpinWaitBehaviour.Balanced && !spinWait.NextSpinWillYield))
                {
                    spinWait.SpinOnce();
                }
                else
                {
                    if (mre.IsSet) mre.Reset();
                    currentLockId = lockId;
                    if (ReaderMustWait(currentLockId, int.MaxValue))
                    {
                        mre.Wait();
                        spinWait.Reset();
                    }
                } 

                currentLockId = lockId;
                newLockId = currentLockId - 1;
            }
            maxReadPressure = 0;
            return new ReadLock(this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<ReadLock> ForReadingAsync()
        {
            return ForReadingAsync(defaultSpinningBehaviour);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async ValueTask<ReadLock> ForReadingAsync(SpinWaitBehaviour spinWaitBehaviour)
        {
            SpinWait spinWait = new SpinWait();
            int myPressure = 0;
            var currentLockId = lockId;
            var newLockId = currentLockId - 1;
            while (ReaderMustWait(currentLockId, myPressure) || currentLockId != Interlocked.CompareExchange(ref lockId, newLockId, currentLockId))
            {
                myPressure++;
                if (myPressure > maxReadPressure) maxReadPressure = myPressure;

                if (spinWaitBehaviour == SpinWaitBehaviour.OnlySpinning ||
                    (spinWaitBehaviour == SpinWaitBehaviour.Balanced && !spinWait.NextSpinWillYield))
                {
                    spinWait.SpinOnce();
                }
                else
                {
                    if (mre.IsSet) mre.Reset();
                    currentLockId = lockId;
                    if (ReaderMustWait(currentLockId, int.MaxValue))
                    {
                        await mre.WaitAsync();
                        spinWait.Reset();
                    }
                }

                currentLockId = lockId;
                newLockId = currentLockId - 1;
            }
            maxReadPressure = 0;
            return new ReadLock(this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ReaderMustWait(int currentLockId, int myPressure)
        {
            return currentLockId > NO_LOCKID || (currentLockId < NO_LOCKID && maxWritePressure >= maxReadPressure) || myPressure < maxReadPressure;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ExitReadLock()
        {
            var newLockId = Interlocked.Increment(ref lockId);
            if (NO_LOCKID == newLockId)
            {
                if (!mre.IsSet) mre.Set();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public WriteLock ForWriting()
        {
            return ForWriting(defaultSpinningBehaviour);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public WriteLock ForWriting(SpinWaitBehaviour spinWaitBehaviour)
        {
            SpinWait spinWait = new SpinWait();
            int myPressure = 0;
            var newLockId = WRITE_LOCKID;
            var currentLockId = lockId;
            while (WriterMustWait(currentLockId, myPressure) || currentLockId != Interlocked.CompareExchange(ref lockId, newLockId, NO_LOCKID))
            {

                myPressure++;
                if (myPressure > maxWritePressure) maxWritePressure = myPressure;

                if (spinWaitBehaviour == SpinWaitBehaviour.OnlySpinning ||
                    (spinWaitBehaviour == SpinWaitBehaviour.Balanced && !spinWait.NextSpinWillYield))
                {
                    spinWait.SpinOnce();
                }
                else
                {
                    if (mre.IsSet) mre.Reset();
                    currentLockId = lockId;
                    if (WriterMustWait(currentLockId, int.MaxValue))
                    {
                        mre.Wait();
                        spinWait.Reset();
                    }
                }

                currentLockId = lockId;
            }
            maxWritePressure = 0;
            return new WriteLock(this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<WriteLock> ForWritingAsync()
        {
            return ForWritingAsync(defaultSpinningBehaviour);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async ValueTask<WriteLock> ForWritingAsync(SpinWaitBehaviour spinWaitBehaviour)
        {
            SpinWait spinWait = new SpinWait();
            int myPressure = 0;
            var newLockId = WRITE_LOCKID;
            var currentLockId = lockId;
            while (WriterMustWait(currentLockId, myPressure) || currentLockId != Interlocked.CompareExchange(ref lockId, newLockId, NO_LOCKID))
            {
                myPressure++;
                if (myPressure > maxWritePressure) maxWritePressure = myPressure;

                if (spinWaitBehaviour == SpinWaitBehaviour.OnlySpinning ||
                    (spinWaitBehaviour == SpinWaitBehaviour.Balanced && !spinWait.NextSpinWillYield))
                {
                    spinWait.SpinOnce();
                }
                else
                {
                    if (mre.IsSet) mre.Reset();
                    currentLockId = lockId;
                    if (WriterMustWait(currentLockId, int.MaxValue))
                    {
                        await mre.WaitAsync();
                        spinWait.Reset();
                    }
                }

                currentLockId = lockId;
            }
            maxWritePressure = 0;
            return new WriteLock(this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool WriterMustWait(int currentLockId, int myPressure)
        {
            return currentLockId != NO_LOCKID || myPressure < maxWritePressure;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ExitWriteLock()
        {
            lockId = NO_LOCKID;
            if (!mre.IsSet) mre.Set();
        }

        public struct ReadLock : IDisposable
        {
            RWLock lockObj;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ReadLock(RWLock safeLock)
            {
                this.lockObj = safeLock;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Dispose()
            {
                lockObj.ExitReadLock();
            }
        }

        public struct WriteLock : IDisposable
        {
            RWLock lockObj;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public WriteLock(RWLock safeLock)
            {
                this.lockObj = safeLock;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Dispose()
            {
                lockObj.ExitWriteLock();
            }
        }
    }
}
