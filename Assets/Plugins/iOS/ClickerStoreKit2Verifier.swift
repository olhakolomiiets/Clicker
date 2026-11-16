import Foundation
import StoreKit

// Raw values are shared with AppleStoreKit2Validator.cs.
private enum ClickerVerificationStatus: Int32 {
    case error = 0
    case verified = 1
    case unverified = 2
    case mismatch = 3
    case notFound = 4
    case unsupportedPlatform = 5
    case ineligible = 6
}

public typealias ClickerStoreKit2Callback =
    @Sendable @convention(c) (Int64, Int32, UnsafePointer<CChar>?, UnsafePointer<CChar>?) -> Void

@_cdecl("ClickerStoreKit2Verify")
public func clickerStoreKit2Verify(
    _ transactionID: UnsafePointer<CChar>?,
    _ productID: UnsafePointer<CChar>?,
    _ productKind: Int32,
    _ requestID: Int64,
    _ callback: @escaping ClickerStoreKit2Callback
) {
    guard #available(iOS 15.0, *) else {
        callback(requestID, ClickerVerificationStatus.unsupportedPlatform.rawValue, nil, nil)
        return
    }

    guard let productID, let product = String(validatingCString: productID), !product.isEmpty,
          let bundleID = Bundle.main.bundleIdentifier, !bundleID.isEmpty else {
        callback(requestID, ClickerVerificationStatus.error.rawValue, nil, nil)
        return
    }

    let expectedID: UInt64?
    if let transactionID {
        guard let text = String(validatingCString: transactionID), let id = UInt64(text),
              id != 0, String(id) == text else {
            callback(requestID, ClickerVerificationStatus.mismatch.rawValue, nil, nil)
            return
        }
        expectedID = id
    } else {
        guard product == "com.planetclicker.noads", productKind == 1 else {
            callback(requestID, ClickerVerificationStatus.ineligible.rawValue, nil, nil)
            return
        }
        expectedID = nil
    }

    let expectedType: Product.ProductType
    switch productKind {
    case 0: expectedType = .consumable
    case 1: expectedType = .nonConsumable
    default:
        callback(requestID, ClickerVerificationStatus.ineligible.rawValue, nil, nil)
        return
    }

    // The input C strings have been copied before yielding to the task.
    Task {
        let (status, transaction) = await verifyTransaction(
            expectedID: expectedID, product: product, expectedType: expectedType, bundleID: bundleID
        )
        if let transaction {
            String(transaction.id).withCString { id in
                transaction.productID.withCString { productID in
                    callback(requestID, status.rawValue, id, productID)
                }
            }
        } else {
            callback(requestID, status.rawValue, nil, nil)
        }
    }
}

@available(iOS 15.0, *)
private func verifyTransaction(
    expectedID: UInt64?,
    product: String,
    expectedType: Product.ProductType,
    bundleID: String
) async -> (ClickerVerificationStatus, Transaction?) {
    let transactions = expectedID == nil ? Transaction.currentEntitlements : Transaction.unfinished
    var rejection: ClickerVerificationStatus = .notFound
    for await result in transactions {
        // An unverified payload can identify a rejection but can never authorize a purchase.
        let candidate = result.unsafePayloadValue
        if let expectedID, candidate.id != expectedID {
            if candidate.productID == product { rejection = .mismatch }
            continue
        }
        guard candidate.productID == product else {
            if expectedID != nil { return (.mismatch, nil) }
            continue
        }
        guard case .verified(let transaction) = result else {
            if expectedID != nil { return (.unverified, nil) }
            rejection = .unverified
            continue
        }
        guard transaction.id != 0, transaction.appBundleID == bundleID,
              transaction.productType == expectedType, transaction.purchasedQuantity == 1,
              transaction.revocationDate == nil else {
            if expectedID != nil { return (.ineligible, nil) }
            rejection = .ineligible
            continue
        }
        return (.verified, transaction)
    }
    return (rejection, nil)
}
