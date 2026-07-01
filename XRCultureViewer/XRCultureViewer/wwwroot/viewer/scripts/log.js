(function () {
    window.log = function (level, msg) {
        // MAUI WebView2 integration
        //window.HybridWebView.InvokeDotNet('OnLogEvent', [level, msg]);
    };
    
    window.logInfo = function (msg) {
        console.info('*** WebGL Viewer: ' + msg)
        window.log('INF', msg);
    };

    window.logWarn = function (msg) {
        console.warn('*** WebGL Viewer: ' + msg)
        window.log('WRN', msg);
    };

    window.logErr = function (msg) {
        if (msg instanceof Error) {
            const errorDetails = {
                message: msg.message,
                name: msg.name,
                stack: msg.stack
            };
            console.error('*** WebGL Viewer Error: ', JSON.stringify(errorDetails));
            window.log('ERR', JSON.stringify(errorDetails));
        } else {
            console.error('*** WebGL Viewer: ' + msg);
            window.log('ERR', msg);
        }
    };
})();