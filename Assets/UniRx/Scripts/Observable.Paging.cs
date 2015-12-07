﻿using System;
using System.Collections.Generic;
using System.Text;
using UniRx.Operators;

namespace UniRx
{
    // Take, Skip, etc..
    public static partial class Observable
    {
        public static IObservable<T> Take<T>(this IObservable<T> source, int count)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (count < 0) throw new ArgumentOutOfRangeException("count");

            if (count == 0) return Empty<T>();

            // optimize .Take(count).Take(count)
            var take = source as TakeObservable<T>;
            if (take != null && take.scheduler == null)
            {
                return take.Combine(count);
            }

            return new TakeObservable<T>(source, count);
        }

        public static IObservable<T> Take<T>(this IObservable<T> source, TimeSpan duration)
        {
            return Take(source, duration, Scheduler.DefaultSchedulers.TimeBasedOperations);
        }

        public static IObservable<T> Take<T>(this IObservable<T> source, TimeSpan duration, IScheduler scheduler)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (scheduler == null) throw new ArgumentNullException("scheduler");

            // optimize .Take(duration).Take(duration)
            var take = source as TakeObservable<T>;
            if (take != null && take.scheduler == scheduler)
            {
                return take.Combine(duration);
            }

            return new TakeObservable<T>(source, duration, scheduler);
        }

        public static IObservable<T> TakeWhile<T>(this IObservable<T> source, Func<T, bool> predicate)
        {
            return new TakeWhileObservable<T>(source, predicate);
        }

        public static IObservable<T> TakeWhile<T>(this IObservable<T> source, Func<T, int, bool> predicate)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (predicate == null) throw new ArgumentNullException("predicate");

            return new TakeWhileObservable<T>(source, predicate);
        }

        public static IObservable<T> TakeUntil<T, TOther>(this IObservable<T> source, IObservable<TOther> other)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (other == null) throw new ArgumentNullException("other");

            return new TakeUntilObservable<T, TOther>(source, other);
        }

        public static IObservable<T> Skip<T>(this IObservable<T> source, int count)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (count < 0) throw new ArgumentOutOfRangeException("count");

            // optimize .Skip(count).Skip(count)
            var skip = source as SkipObservable<T>;
            if (skip != null && skip.scheduler == null)
            {
                return skip.Combine(count);
            }

            return new SkipObservable<T>(source, count);
        }

        public static IObservable<T> Skip<T>(this IObservable<T> source, TimeSpan duration)
        {
            return Skip(source, duration, Scheduler.DefaultSchedulers.TimeBasedOperations);
        }

        public static IObservable<T> Skip<T>(this IObservable<T> source, TimeSpan duration, IScheduler scheduler)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (scheduler == null) throw new ArgumentNullException("scheduler");

            // optimize .Skip(duration).Skip(duration)
            var skip = source as SkipObservable<T>;
            if (skip != null && skip.scheduler == scheduler)
            {
                return skip.Combine(duration);
            }

            return new SkipObservable<T>(source, duration, scheduler);
        }

        public static IObservable<T> SkipWhile<T>(this IObservable<T> source, Func<T, bool> predicate)
        {
            return new SkipWhileObservable<T>(source, predicate);
        }

        public static IObservable<T> SkipWhile<T>(this IObservable<T> source, Func<T, int, bool> predicate)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (predicate == null) throw new ArgumentNullException("predicate");

            return new SkipWhileObservable<T>(source, predicate);
        }

        public static IObservable<T> SkipUntil<T, TOther>(this IObservable<T> source, IObservable<TOther> other)
        {
            return new SkipUntilObservable<T, TOther>(source, other);
        }

        public static IObservable<IList<T>> Buffer<T>(this IObservable<T> source, int count)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (count <= 0) throw new ArgumentOutOfRangeException("count <= 0");

            return Observable.Create<IList<T>>(observer =>
            {
                var list = new List<T>();

                return source.Subscribe(x =>
                {
                    list.Add(x);
                    if (list.Count == count)
                    {
                        observer.OnNext(list);
                        list = new List<T>();
                    }
                }, observer.OnError, () =>
                {
                    if (list.Count > 0)
                    {
                        observer.OnNext(list);
                    }
                    observer.OnCompleted();
                });
            });
        }

        public static IObservable<IList<T>> Buffer<T>(this IObservable<T> source, int count, int skip)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (count <= 0) throw new ArgumentOutOfRangeException("count <= 0");
            if (skip <= 0) throw new ArgumentOutOfRangeException("skip <= 0");

            return Observable.Create<IList<T>>(observer =>
            {
                var q = new Queue<List<T>>();

                var index = -1;
                return source.Subscribe(x =>
                {
                    index++;

                    if (index % skip == 0)
                    {
                        q.Enqueue(new List<T>(count));
                    }

                    var len = q.Count;
                    for (int i = 0; i < len; i++)
                    {
                        var list = q.Dequeue();
                        list.Add(x);
                        if (list.Count == count)
                        {
                            observer.OnNext(list);
                        }
                        else
                        {
                            q.Enqueue(list);
                        }
                    }
                }, observer.OnError, () =>
                {
                    foreach (var list in q)
                    {
                        observer.OnNext(list);
                    }
                    observer.OnCompleted();
                });
            });
        }

        public static IObservable<IList<T>> Buffer<T>(this IObservable<T> source, TimeSpan timeSpan)
        {
            return Buffer(source, timeSpan, Scheduler.DefaultSchedulers.TimeBasedOperations);
        }

        public static IObservable<IList<T>> Buffer<T>(this IObservable<T> source, TimeSpan timeSpan, IScheduler scheduler)
        {
            if (source == null) throw new ArgumentNullException("source");

            return Observable.Create<IList<T>>(observer =>
            {
                var list = new List<T>();
                var gate = new object();

                var d = new CompositeDisposable(2);

                // timer
                d.Add(scheduler.Schedule(timeSpan, self =>
                {
                    List<T> currentList;
                    lock (gate)
                    {
                        currentList = list;
                        list = new List<T>();
                    }

                    observer.OnNext(currentList);
                    self(timeSpan);
                }));

                // subscription
                d.Add(source.Subscribe(x =>
                {
                    lock (gate)
                    {
                        list.Add(x);
                    }
                }, observer.OnError, () =>
                {
                    var currentList = list;
                    observer.OnNext(currentList);
                    observer.OnCompleted();
                }));

                return d;
            });
        }

        public static IObservable<IList<T>> Buffer<T>(this IObservable<T> source, TimeSpan timeSpan, int count)
        {
            return Buffer(source, timeSpan, count, Scheduler.DefaultSchedulers.TimeBasedOperations);
        }

        public static IObservable<IList<T>> Buffer<T>(this IObservable<T> source, TimeSpan timeSpan, int count, IScheduler scheduler)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (count <= 0) throw new ArgumentOutOfRangeException("count <= 0");

            return Observable.Create<IList<T>>(observer =>
            {
                var list = new List<T>();
                var gate = new object();
                var timerId = 0L;

                var d = new CompositeDisposable(2);
                var timerD = new SerialDisposable();

                // timer
                d.Add(timerD);
                Action createTimer = () =>
                {
                    var currentTimerId = timerId;
                    var timerS = new SingleAssignmentDisposable();
                    timerD.Disposable = timerS; // restart timer(dispose before)
                    timerS.Disposable = scheduler.Schedule(timeSpan, self =>
                    {
                        List<T> currentList;
                        lock (gate)
                        {
                            if (currentTimerId != timerId) return;

                            currentList = list;
                            if (currentList.Count != 0)
                            {
                                list = new List<T>();
                            }
                        }
                        if (currentList.Count != 0)
                        {
                            observer.OnNext(currentList);
                        }
                        self(timeSpan);
                    });
                };

                createTimer();

                // subscription
                d.Add(source.Subscribe(x =>
                {
                    List<T> currentList = null;
                    lock (gate)
                    {
                        list.Add(x);
                        if (list.Count == count)
                        {
                            currentList = list;
                            list = new List<T>();
                            timerId++;
                            createTimer();
                        }
                    }
                    if (currentList != null)
                    {
                        observer.OnNext(currentList);
                    }
                }, observer.OnError, () =>
                {
                    lock (gate)
                    {
                        timerId++;
                    }
                    var currentList = list;
                    observer.OnNext(currentList);
                    observer.OnCompleted();
                }));

                return d;
            });
        }

        public static IObservable<IList<T>> Buffer<T>(this IObservable<T> source, TimeSpan timeSpan, TimeSpan timeShift)
        {
            return Buffer(source, timeSpan, timeShift, Scheduler.DefaultSchedulers.TimeBasedOperations);
        }

        public static IObservable<IList<T>> Buffer<T>(this IObservable<T> source, TimeSpan timeSpan, TimeSpan timeShift, IScheduler scheduler)
        {
            if (source == null) throw new ArgumentNullException("source");

            return Observable.Create<IList<T>>(observer =>
            {
                var totalTime = TimeSpan.Zero;
                var nextShift = timeShift;
                var nextSpan = timeSpan;

                var gate = new object();
                var q = new Queue<IList<T>>();

                var timerD = new SerialDisposable();

                var createTimer = default(Action);
                createTimer = () =>
                {
                    var m = new SingleAssignmentDisposable();
                    timerD.Disposable = m;

                    var isSpan = false;
                    var isShift = false;
                    if (nextSpan == nextShift)
                    {
                        isSpan = true;
                        isShift = true;
                    }
                    else if (nextSpan < nextShift)
                        isSpan = true;
                    else
                        isShift = true;

                    var newTotalTime = isSpan ? nextSpan : nextShift;
                    var ts = newTotalTime - totalTime;
                    totalTime = newTotalTime;

                    if (isSpan)
                        nextSpan += timeShift;
                    if (isShift)
                        nextShift += timeShift;

                    m.Disposable = scheduler.Schedule(ts, () =>
                    {
                        lock (gate)
                        {
                            if (isShift)
                            {
                                var s = new List<T>();
                                q.Enqueue(s);
                            }
                            if (isSpan)
                            {
                                var s = q.Dequeue();
                                observer.OnNext(s);
                            }
                        }

                        createTimer();
                    });
                };

                q.Enqueue(new List<T>());

                createTimer();

                return source.Subscribe(
                    x =>
                    {
                        lock (gate)
                        {
                            foreach (var s in q)
                                s.Add(x);
                        }
                    },
                    observer.OnError,
                    () =>
                    {
                        lock (gate)
                        {
                            foreach (var list in q)
                            {
                                observer.OnNext(list);
                            }

                            observer.OnCompleted();
                        }
                    }
                );
            });
        }

        public static IObservable<IList<TSource>> Buffer<TSource, TWindowBoundary>(this IObservable<TSource> source, IObservable<TWindowBoundary> windowBoundaries)
        {
            return Observable.Create<IList<TSource>>(observer =>
            {
                var list = new List<TSource>();
                var gate = new object();

                var d = new CompositeDisposable(2);

                d.Add(source.Subscribe(Observer.Create<TSource>(
                    x =>
                    {
                        lock (gate)
                        {
                            list.Add(x);
                        }
                    },
                    ex =>
                    {
                        lock (gate)
                        {
                            observer.OnError(ex);
                        }
                    },
                    () =>
                    {
                        lock (gate)
                        {
                            var currentList = list;
                            list = new List<TSource>(); // safe
                            observer.OnNext(currentList);
                            observer.OnCompleted();
                        }
                    }
                )));

                d.Add(windowBoundaries.Subscribe(Observer.Create<TWindowBoundary>(
                    w =>
                    {
                        List<TSource> currentList;
                        lock (gate)
                        {
                            currentList = list;
                            if (currentList.Count != 0)
                            {
                                list = new List<TSource>();
                            }
                        }
                        if (currentList.Count != 0)
                        {
                            observer.OnNext(currentList);
                        }
                    },
                    ex =>
                    {
                        lock (gate)
                        {
                            observer.OnError(ex);
                        }
                    },
                    () =>
                    {
                        lock (gate)
                        {
                            var currentList = list;
                            list = new List<TSource>(); // safe
                            observer.OnNext(currentList);
                            observer.OnCompleted();
                        }
                    }
                )));

                return d;
            });
        }

        /// <summary>Projects old and new element of a sequence into a new form.</summary>
        public static IObservable<TR> Pairwise<T, TR>(this IObservable<T> source, Func<T, T, TR> selector)
        {
            return new PairwiseObservable<T, TR>(source, selector);
        }

        // first, last, single

        public static IObservable<T> Last<T>(this IObservable<T> source)
        {
            return new LastObservable<T>(source, false);
        }
        public static IObservable<T> Last<T>(this IObservable<T> source, Func<T, bool> predicate)
        {
            return new LastObservable<T>(source, predicate, false);
        }

        public static IObservable<T> LastOrDefault<T>(this IObservable<T> source)
        {
            return new LastObservable<T>(source, true);
        }

        public static IObservable<T> LastOrDefault<T>(this IObservable<T> source, Func<T, bool> predicate)
        {
            return new LastObservable<T>(source, predicate, true);
        }

        public static IObservable<T> First<T>(this IObservable<T> source)
        {
            return new FirstObservable<T>(source, false);
        }
        public static IObservable<T> First<T>(this IObservable<T> source, Func<T, bool> predicate)
        {
            return new FirstObservable<T>(source, predicate, false);
        }

        public static IObservable<T> FirstOrDefault<T>(this IObservable<T> source)
        {
            return new FirstObservable<T>(source, true);
        }

        public static IObservable<T> FirstOrDefault<T>(this IObservable<T> source, Func<T, bool> predicate)
        {
            return new FirstObservable<T>(source, predicate, true);
        }

        public static IObservable<T> Single<T>(this IObservable<T> source)
        {
            return new SingleObservable<T>(source, false);
        }
        public static IObservable<T> Single<T>(this IObservable<T> source, Func<T, bool> predicate)
        {
            return new SingleObservable<T>(source, predicate, false);
        }

        public static IObservable<T> SingleOrDefault<T>(this IObservable<T> source)
        {
            return new SingleObservable<T>(source, true);
        }

        public static IObservable<T> SingleOrDefault<T>(this IObservable<T> source, Func<T, bool> predicate)
        {
            return new SingleObservable<T>(source, predicate, true);
        }
    }
}