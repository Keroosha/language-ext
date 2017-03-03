﻿using System;
using System.Collections.Generic;
using System.Linq;
using LanguageExt.TypeClasses;
using System.Diagnostics.Contracts;
using static LanguageExt.Prelude;
using System.Threading.Tasks;

namespace LanguageExt.ClassInstances
{
    public struct MTryOptionAsync<A> :
        Optional<TryOptionAsync<A>, A>,
        MonadPlus<TryOptionAsync<A>, A>,
        Foldable<TryOptionAsync<A>, A>,
        BiFoldable<TryOptionAsync<A>, Unit, A>
    {
        public static readonly MTryOptionAsync<A> Inst = default(MTryOptionAsync<A>);

        static TryOptionAsync<A> none = () => throw new BottomException();

        [Pure]
        public TryOptionAsync<A> None => none;

        [Pure]
        public MB Bind<MONADB, MB, B>(TryOptionAsync<A> ma, Func<A, MB> f) where MONADB : struct, Monad<MB, B>
        {
            if (typeof(MB) == typeof(TryOptionAsync<B>) && typeof(MONADB) == typeof(MTryOptionAsync<B>))
            {
                // TODO: This is a hack to get around the type system for async
                //       A better solution is needed

                var mb = from a in ma
                         from b in (TryOptionAsync<B>)(object)f(a)
                         select b;

                return (MB)(object)mb;
            }
            else
            {
                // Synchronous type-safe version
                return ma.Match(
                    Some: f,
                    None: () => default(MONADB).Fail(),           
                    Fail: x => default(MONADB).Fail(x)).Result;
            }
        }

        [Pure]
        public TryOptionAsync<A> Fail(object err) =>
            TryOptionAsync<A>(() => { throw new BottomException(); });

        [Pure]
        public TryOptionAsync<A> Fail(Exception err = null) =>
            TryOptionAsync<A>(() => { throw err; });

        [Pure]
        public TryOptionAsync<A> Plus(TryOptionAsync<A> ma, TryOptionAsync<A> mb) => async () =>
        {
            // Run in parallel
            var resA = ma.Try();
            var resB = mb.Try();

            await Task.WhenAll(resA, resB);
            return (!resA.IsFaulted && !resA.Result.IsFaulted) 
                ? resA.Result
                : resB.Result;
        };

        /// <summary>
        /// Monad return
        /// </summary>
        /// <param name="xs">The bound monad value(s)</param>
        /// <returns>Monad of A</returns>
        [Pure]
        public TryOptionAsync<A> FromSeq(IEnumerable<A> xs)
        {
            var head = xs.FirstOrDefault();
            return Return(head);
        }

        /// <summary>
        /// Monad return
        /// </summary>
        /// <typeparam name="A">Type of the bound monad value</typeparam>
        /// <param name="x">The bound monad value</param>
        /// <returns>Monad of A</returns>
        [Pure]
        public TryOptionAsync<A> Return(A x) =>
            () => Task.Run(() => new OptionalResult<A>(x));

        /// <summary>
        /// Monad return
        /// </summary>
        /// <param name="f">The function to invoke to get the bound monad value(s)</param>
        /// <returns>Monad of A</returns>
        [Pure]
        public TryOptionAsync<A> Return(Func<A> f) => 
            () => Task.Run(() => new OptionalResult<A>(f()));

        [Pure]
        public TryOptionAsync<A> Zero() => 
            none;

        [Pure]
        public bool IsNone(TryOptionAsync<A> opt) =>
            !IsSome(opt);

        [Pure]
        public bool IsSome(TryOptionAsync<A> opt) =>
            Match(opt, Some: _ => true, None: () => false);

        [Pure]
        public bool IsUnsafe(TryOptionAsync<A> opt) =>
            true;

        [Pure]
        public B Match<B>(TryOptionAsync<A> opt, Func<A, B> Some, Func<B> None)
        {
            try
            {
                var res = opt.Try().Result; // TODO: No asynchrony
                if (res.IsFaultedOrNone)
                    return None();
                else
                    return Some(res.Value.Value);
            }
            catch
            {
                return None();
            }
        }

        public Unit Match(TryOptionAsync<A> opt, Action<A> Some, Action None) =>
            Match(opt,
                x  => { Some(x); return unit; },
                () => { None(); return unit; });

        [Pure]
        public B MatchUnsafe<B>(TryOptionAsync<A> opt, Func<A, B> Some, Func<B> None) =>
            Match(opt, Some, None);

        [Pure]
        public S Fold<S>(TryOptionAsync<A> ma, S state, Func<S, A, S> f) =>
            Match(ma, x => f(state, x), () => state);

        [Pure]
        public S FoldBack<S>(TryOptionAsync<A> ma, S state, Func<S, A, S> f) =>
            Match(ma, x => f(state, x), () => state);

        [Pure]
        public S BiFold<S>(TryOptionAsync<A> ma, S state, Func<S, Unit, S> fa, Func<S, A, S> fb) =>
            Match(ma, x => fb(state, x), () => fa(state, unit));

        [Pure]
        public S BiFoldBack<S>(TryOptionAsync<A> ma, S state, Func<S, Unit, S> fa, Func<S, A, S> fb) =>
            Match(ma, x => fb(state, x), () => fa(state, unit));

        [Pure]
        public int Count(TryOptionAsync<A> ma) =>
            Match(ma, x => 1, () => 0);

        [Pure]
        public TryOptionAsync<A> Some(A value) =>
            Return(value);

        [Pure]
        public TryOptionAsync<A> Optional(A value) =>
            Return(value);
    }
}