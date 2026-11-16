using System.Threading.Tasks;
using UnityEngine.Purchasing;

#if UNITY_IOS && !UNITY_EDITOR
using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;
#endif

namespace Samples.Purchasing.IAP5.Demo
{
    public static class AppleStoreKit2Validator
    {
        // Raw values are shared with ClickerStoreKit2Verifier.swift.
        public enum VerificationStatus
        {
            Error = 0,
            Verified = 1,
            Unverified = 2,
            Mismatch = 3,
            NotFound = 4,
            UnsupportedPlatform = 5,
            Ineligible = 6
        }

        public readonly struct Result
        {
            public VerificationStatus Status { get; }
            public string TransactionId { get; }
            public string StoreProductId { get; }

            internal Result(VerificationStatus status, string transactionId = null, string storeProductId = null)
            {
                Status = status;
                TransactionId = transactionId;
                StoreProductId = storeProductId;
            }
        }

        public static Task<Result> VerifyPendingOrderAsync(PendingOrder order)
        {
#if UNITY_IOS && !UNITY_EDITOR
            var info = order?.Info;
            var items = order?.CartOrdered?.Items();
            if (info?.Apple?.StoreName != AppleAppStore.Name || !IsTransactionId(info.TransactionID) ||
                items == null || items.Count != 1 || items[0]?.Quantity != 1)
                return Task.FromResult(new Result(VerificationStatus.Mismatch));

            var product = items[0].Product?.definition;
            if (string.IsNullOrWhiteSpace(product?.id) || string.IsNullOrWhiteSpace(product.storeSpecificId) ||
                (product.type != ProductType.Consumable && product.type != ProductType.NonConsumable))
                return Task.FromResult(new Result(VerificationStatus.Ineligible));

            int productKind = product.type == ProductType.Consumable ? 0 : 1;
            return VerifyAsync(info.TransactionID, product.storeSpecificId, productKind);
#else
            return Task.FromResult(new Result(VerificationStatus.UnsupportedPlatform));
#endif
        }

        public static Task<Result> VerifyNoAdsEntitlementAsync()
        {
#if UNITY_IOS && !UNITY_EDITOR
            return VerifyAsync(null, SaveSystem.NoAdsProductId, 1);
#else
            return Task.FromResult(new Result(VerificationStatus.UnsupportedPlatform));
#endif
        }

#if UNITY_IOS && !UNITY_EDITOR
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void NativeCallback(long requestId, int code, IntPtr transactionId, IntPtr productId);

        private static readonly NativeCallback s_Callback = OnNativeResult;
        private static readonly ConcurrentDictionary<long, (TaskCompletionSource<Result> Completion,
            string TransactionId, string ProductId)> s_Requests = new();
        private static SynchronizationContext s_UnityContext;
        private static long s_NextRequestId;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CaptureUnityContext()
        {
            s_UnityContext = SynchronizationContext.Current;
        }

        [DllImport("__Internal", EntryPoint = "ClickerStoreKit2Verify", CallingConvention = CallingConvention.Cdecl)]
        private static extern void VerifyNative(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string transactionId,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string productId,
            int productKind, long requestId, NativeCallback callback);

        private static Task<Result> VerifyAsync(string transactionId, string productId, int productKind)
        {
            if (s_UnityContext == null)
                return Task.FromResult(new Result(VerificationStatus.Error));

            var completion = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
            long requestId = Interlocked.Increment(ref s_NextRequestId);
            if (!s_Requests.TryAdd(requestId, (completion, transactionId, productId)))
                return Task.FromResult(new Result(VerificationStatus.Error));

            try
            {
                VerifyNative(transactionId, productId, productKind, requestId, s_Callback);
            }
            catch (Exception)
            {
                if (s_Requests.TryRemove(requestId, out _))
                    completion.TrySetResult(new Result(VerificationStatus.Error));
            }

            return completion.Task;
        }

        [AOT.MonoPInvokeCallback(typeof(NativeCallback))]
        private static void OnNativeResult(long requestId, int code, IntPtr transactionId, IntPtr productId)
        {
            if (!s_Requests.TryRemove(requestId, out var request))
                return;

            var result = new Result(VerificationStatus.Error);
            try
            {
                if (code == (int)VerificationStatus.Verified)
                {
                    // Swift owns these buffers for the duration of this callback.
                    string id = Marshal.PtrToStringUTF8(transactionId);
                    string product = Marshal.PtrToStringUTF8(productId);
                    result = IsTransactionId(id) && (request.TransactionId == null || id == request.TransactionId)
                        && product == request.ProductId
                        ? new Result(VerificationStatus.Verified, id, product)
                        : new Result(VerificationStatus.Mismatch);
                }
                else if (Enum.IsDefined(typeof(VerificationStatus), code))
                {
                    result = new Result((VerificationStatus)code);
                }

                s_UnityContext.Post(_ => request.Completion.TrySetResult(result), null);
            }
            catch (Exception)
            {
                request.Completion.TrySetResult(new Result(VerificationStatus.Error));
            }
        }

        private static bool IsTransactionId(string value)
        {
            return ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out ulong id) &&
                   id != 0 && value == id.ToString(CultureInfo.InvariantCulture);
        }
#endif
    }
}
