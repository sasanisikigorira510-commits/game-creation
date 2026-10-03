#import <AuthenticationServices/AuthenticationServices.h>
#import <UIKit/UIKit.h>
extern "C" void UnitySendMessage(const char *, const char *, const char *);
extern "C" UIViewController *UnityGetGLViewController(void);

@interface NasusAppleIdentity : NSObject <ASAuthorizationControllerDelegate, ASAuthorizationControllerPresentationContextProviding>
@property(nonatomic, copy) NSString *receiver;
@property(nonatomic, copy) NSString *state;
@property(nonatomic, strong) ASAuthorizationController *controller;
@property(nonatomic, strong) UIWindow *window;
@end

static NasusAppleIdentity *activeIdentity;
@implementation NasusAppleIdentity
- (ASPresentationAnchor)presentationAnchorForAuthorizationController:(ASAuthorizationController *)controller {
    return self.window;
}
- (void)finish:(NSString *)token code:(NSString *)code error:(NSString *)error {
    if (activeIdentity != self) return;
    NSDictionary *response = @{ @"State": self.state ?: @"", @"IdentityToken": token ?: @"",
                               @"AuthorizationCode": code ?: @"", @"Error": error ?: @"" };
    NSData *data = [NSJSONSerialization dataWithJSONObject:response options:0 error:nil];
    NSString *json = [[NSString alloc] initWithData:data encoding:NSUTF8StringEncoding];
    // Never NSLog JWT, subject, authorizationCode or Apple's error description.
    UnitySendMessage(self.receiver.UTF8String, "OnAppleIdentity", json.UTF8String);
    self.controller.delegate = nil;
    self.controller.presentationContextProvider = nil;
    self.controller = nil;
    activeIdentity = nil;
}
- (void)authorizationController:(ASAuthorizationController *)controller didCompleteWithAuthorization:(ASAuthorization *)authorization {
    if (![authorization.credential isKindOfClass:[ASAuthorizationAppleIDCredential class]]) {
        [self finish:nil code:nil error:@"認証の種類を確認できませんでした。"]; return;
    }
    ASAuthorizationAppleIDCredential *credential = (ASAuthorizationAppleIDCredential *)authorization.credential;
    if (![credential.state isEqualToString:self.state] || credential.identityToken.length == 0 || credential.authorizationCode.length == 0) {
        [self finish:nil code:nil error:@"認証応答が一致しません。再試行してください。"]; return;
    }
    [self finish:[[NSString alloc] initWithData:credential.identityToken encoding:NSUTF8StringEncoding]
            code:[[NSString alloc] initWithData:credential.authorizationCode encoding:NSUTF8StringEncoding] error:nil];
}
- (void)authorizationController:(ASAuthorizationController *)controller didCompleteWithError:(NSError *)error {
    [self finish:nil code:nil error:error.code == ASAuthorizationErrorCanceled ? @"キャンセルしました。" : @"Apple認証に失敗しました。通信と設定をご確認ください。"];
}
@end

extern "C" void NasusAppleSignIn(const char *receiver, const char *nonce, const char *state) {
    NSString *target = [NSString stringWithUTF8String:receiver];
    NSString *challenge = [NSString stringWithUTF8String:state];
    NSString *serverNonce = [NSString stringWithUTF8String:nonce];
    dispatch_async(dispatch_get_main_queue(), ^{
        if (activeIdentity != nil) return; // C# serializes callers and has a timeout.
        activeIdentity = [NasusAppleIdentity new];
        activeIdentity.receiver = target;
        activeIdentity.state = challenge;
        activeIdentity.window = UnityGetGLViewController().view.window;
        if (!activeIdentity.window) { [activeIdentity finish:nil code:nil error:@"認証画面を表示できませんでした。"]; return; }
        ASAuthorizationAppleIDRequest *request = [[ASAuthorizationAppleIDProvider new] createRequest];
        request.nonce = serverNonce;
        request.state = challenge;
        request.requestedScopes = @[];
        activeIdentity.controller = [[ASAuthorizationController alloc] initWithAuthorizationRequests:@[request]];
        activeIdentity.controller.delegate = activeIdentity;
        activeIdentity.controller.presentationContextProvider = activeIdentity;
        [activeIdentity.controller performRequests];
    });
}
extern "C" void NasusAppleCancel(const char *state) {
    NSString *challenge = [NSString stringWithUTF8String:state];
    dispatch_async(dispatch_get_main_queue(), ^{
        if (![activeIdentity.state isEqualToString:challenge]) return;
        if (@available(iOS 16.0, *)) [activeIdentity.controller cancel];
        [activeIdentity finish:nil code:nil error:@"認証を終了しました。"];
    });
}
