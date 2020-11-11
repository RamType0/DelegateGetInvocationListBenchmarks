using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DelegateGetInvocationListBenchmarks
{
    public class Benchmarks
    {
        
        static void Main(string[] args)
        {
            BenchmarkRunner.Run<Benchmarks>();
        }
        [Params(1, 10, 100, 1000)]
        public int N;

        private MyAction action = default!;

        [GlobalSetup]
        public void GlobalSetup()
        {
            action = new MyAction(N);
        }
        [Benchmark]
        public void CoreCLRGetInvocationList()
        {
            foreach (var d in action.CoreCLRGetInvocationList())
            {
                var act = (MyAction)d;
                act.Invoke();
            }
        }
        [Benchmark]
        public void MyGetInvocationList()
        {
            foreach (var d in action.MyGetInvocationList())
            {
                var act = (MyAction)d;
                act.Invoke();
            }
        }
        [Benchmark]
        public void UnsafeGetInvocationList()
        {
            foreach (var d in MyDelegateMarshal.UnsafeGetInvocationList(action))
            {
                d.Invoke();
            }
        }

    }

    public abstract class MyDelegate
    {
        public abstract MyDelegate[] CoreCLRGetInvocationList();
        public abstract MyDelegate[] MyGetInvocationList();
    }

    public abstract class MyMulticastDelegate : MyDelegate
    {
        internal object? _invocationList;
        internal IntPtr _invocationCount;
        /// <summary>
        /// Copy from CoreCLR implementation.
        /// </summary>
        /// <returns></returns>
        public sealed override MyDelegate[] CoreCLRGetInvocationList()
        {
           MyDelegate[] del;
            if (!(_invocationList is object[] invocationList))
            {
                del = new MyDelegate[1];
                del[0] = this;
            }
            else
            {
                // Create an array of delegate copies and each
                //    element into the array
                del = new MyDelegate[(int)_invocationCount];

                for (int i = 0; i < del.Length; i++)
                    del[i] = (MyDelegate)invocationList[i];
            }
            return del;
        }
        /// <summary>
        /// My implementation.
        /// </summary>
        /// <returns></returns>
        public sealed override MyDelegate[] MyGetInvocationList()
        {
            var _invocationList = this._invocationList;
            if (_invocationList != null)
            {
                var invocationList = Unsafe.As<object[]>(_invocationList);
                return MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<object, MyDelegate>(ref MemoryMarshal.GetArrayDataReference(invocationList)), (int)_invocationCount).ToArray();
            }
            else
            {
                return new MyDelegate[] { this };
            }
        }
    }
    public sealed class MyAction : MyMulticastDelegate
    {
        
        public MyAction(int count)
        {
            if (count > 1)
            {
                var invocationList = new object[count];
                for (int i = 0; i < invocationList.Length; i++)
                {
                    invocationList[i] = new MyAction();
                }
                _invocationList = invocationList;
                _invocationCount = (IntPtr)count;
            }
        }
        public MyAction() { }
        public void Invoke() { }
    }
    

    public static class MyDelegateMarshal
    {
        public static ReadOnlySpan<T> UnsafeGetInvocationList<T>(in T d)
            where T: MyMulticastDelegate
        {
            var _invocationList = d._invocationList;
            if(_invocationList != null)
            {
                var invocationList = Unsafe.As<object[]>(_invocationList);
                return MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<object, T>(ref MemoryMarshal.GetArrayDataReference(invocationList)), (int)d._invocationCount);
            }
            else
            {
                return MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(d), 1);
            }
        }
    }
    
}
