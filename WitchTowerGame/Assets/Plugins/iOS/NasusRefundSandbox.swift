import Foundation
import StoreKit
import UIKit

@_silgen_name("UnitySendMessage")
private func sendUnityMessage(_ object: UnsafePointer<CChar>, _ method: UnsafePointer<CChar>, _ message: UnsafePointer<CChar>)

@_cdecl("NasusRefundSandboxRequest")
public func nasusRefundSandboxRequest(_ transaction: UnsafePointer<CChar>?, _ receiver: UnsafePointer<CChar>?) {
    guard let transaction, let receiver else { return }
    let transactionText = String(cString: transaction)
    let receiverText = String(cString: receiver)
    Task { @MainActor in
        func complete(_ message: String) {
            receiverText.withCString { object in
                "OnRefundResult".withCString { method in
                    message.withCString { value in sendUnityMessage(object, method, value) }
                }
            }
        }
        guard #available(iOS 16.0, *), let transactionID = UInt64(transactionText) else {
            complete("unavailable"); return
        }
        do {
            // Never show refund UI for a production storefront transaction.
            guard case .verified(let app) = try await AppTransaction.shared,
                  app.environment == .sandbox,
                  app.bundleID == "com.nasus.dungeonmonsterroguelike" else {
                complete("not_sandbox"); return
            }
            guard let scene = UIApplication.shared.connectedScenes.compactMap({ $0 as? UIWindowScene })
                .first(where: { $0.activationState == .foregroundActive }) else {
                complete("unavailable"); return
            }
            let status = try await Transaction.beginRefundRequest(for: transactionID, in: scene)
            switch status {
            case .success: complete("submitted")
            case .userCancelled: complete("cancelled")
            @unknown default: complete("unavailable")
            }
        } catch {
            // No receipts, account details, or signed payloads in Unity logs.
            complete("failed")
        }
    }
}
