import Foundation
import StoreKit

@_silgen_name("UnitySendMessage")
private func sendPurchaseEnvironmentToUnity(_ object: UnsafePointer<CChar>, _ method: UnsafePointer<CChar>, _ message: UnsafePointer<CChar>)

// Linearize bridge calls before scheduling actor work: unstructured Tasks do
// not guarantee FIFO. A cancelled/older queued begin must not cancel a newer
// task or start after its Unity owner has already gone away.
private final class PurchaseEnvironmentBridgeState: @unchecked Sendable {
    static let shared = PurchaseEnvironmentBridgeState()
    private let lock = NSLock()
    private var request: String?
    func begin(_ value: String) {
        lock.lock(); defer { lock.unlock() }
        request = value
    }
    func cancel(_ value: String) {
        lock.lock(); defer { lock.unlock() }
        if request == value { request = nil }
    }
    func isCurrent(_ value: String) -> Bool {
        lock.lock(); defer { lock.unlock() }
        return request == value
    }
    func finish(_ value: String) -> Bool {
        lock.lock(); defer { lock.unlock() }
        guard request == value else { return false }
        request = nil
        return true
    }
}

@MainActor
private enum PurchaseEnvironmentProbeTask {
    static var request: String?
    static var current: Task<Void, Never>?
    static func cancel() {
        current?.cancel()
        current = nil
        request = nil
    }
}

@_cdecl("NasusApplePurchaseEnvironmentCancel")
public func nasusApplePurchaseEnvironmentCancel(_ request: UnsafePointer<CChar>?) {
    guard let request else { return }
    let requestText = String(cString: request)
    PurchaseEnvironmentBridgeState.shared.cancel(requestText)
    Task { @MainActor in
        if PurchaseEnvironmentProbeTask.request == requestText { PurchaseEnvironmentProbeTask.cancel() }
    }
}

@_cdecl("NasusApplePurchaseEnvironment")
public func nasusApplePurchaseEnvironment(_ receiver: UnsafePointer<CChar>?, _ request: UnsafePointer<CChar>?) {
    beginPurchaseEnvironmentProbe(receiver, request, userInitiated: false)
}

// AppTransaction.refresh may ask the user to authenticate. This entry point is
// called only by the visible recovery button, never by startup/foreground retry.
@_cdecl("NasusApplePurchaseEnvironmentRefresh")
public func nasusApplePurchaseEnvironmentRefresh(_ receiver: UnsafePointer<CChar>?, _ request: UnsafePointer<CChar>?) {
    beginPurchaseEnvironmentProbe(receiver, request, userInitiated: true)
}

private func beginPurchaseEnvironmentProbe(_ receiver: UnsafePointer<CChar>?, _ request: UnsafePointer<CChar>?, userInitiated: Bool) {
    guard let receiver, let request else { return }
    let receiverText = String(cString: receiver)
    let requestText = String(cString: request)
    guard requestText.count == 32, requestText.utf8.allSatisfy({ (48...57).contains($0) || (97...102).contains($0) }) else { return }
    PurchaseEnvironmentBridgeState.shared.begin(requestText)
    NSLog("[AppleEnvironmentNative] %@", userInitiated ? "RefreshRequested" : "SharedRequested")
    Task { @MainActor in
        guard PurchaseEnvironmentBridgeState.shared.isCurrent(requestText) else { return }
        PurchaseEnvironmentProbeTask.cancel()
        PurchaseEnvironmentProbeTask.request = requestText
        NSLog("[AppleEnvironmentNative] Executing")
        PurchaseEnvironmentProbeTask.current = Task { @MainActor in
            @MainActor func complete(_ environment: String, _ reason: String) {
                guard !Task.isCancelled, PurchaseEnvironmentProbeTask.request == requestText,
                      PurchaseEnvironmentBridgeState.shared.finish(requestText) else { return }
                // No JWS, receipt, user/account data or exception details cross this
                // boundary. The server remains the purchase-delivery authority.
                NSLog("[AppleEnvironmentNative] %@ / %@", reason, environment)
                let payload = "{\"RequestId\":\"\(requestText)\",\"Environment\":\"\(environment)\",\"Reason\":\"\(reason)\"}"
                receiverText.withCString { object in
                    "OnApplePurchaseEnvironment".withCString { method in
                        payload.withCString { value in sendPurchaseEnvironmentToUnity(object, method, value) }
                    }
                }
                PurchaseEnvironmentProbeTask.current = nil
                PurchaseEnvironmentProbeTask.request = nil
            }
            guard #available(iOS 16.0, *) else { complete("Unsupported", "Unsupported"); return }
            do {
                let result = try await (userInitiated ? AppTransaction.refresh() : AppTransaction.shared)
                guard case .verified(let app) = result else {
                    complete("Unknown", "Unverified"); return
                }
                guard app.bundleID == "com.nasus.dungeonmonsterroguelike" else {
                    complete("Unknown", "BundleMismatch"); return
                }
                if app.environment == .production { complete("Production", "Verified") }
                else if app.environment == .sandbox { complete("Sandbox", "Verified") }
                else { complete("Unknown", "UnknownEnvironment") }
            } catch let error as StoreKitError {
                // Fixed categories only; never print localized errors, account
                // information, receipts, JWS or arbitrary StoreKit userInfo.
                switch error {
                case .userCancelled: complete("Unknown", "Cancelled")
                case .networkError: complete("Unknown", "Network")
                case .notAvailableInStorefront: complete("Unknown", "StoreUnavailable")
                case .notEntitled: complete("Unknown", "NotEntitled")
                default: complete("Unknown", "StoreKitFailure")
                }
            } catch { complete("Unknown", "StoreKitFailure") }
        }
    }
}
