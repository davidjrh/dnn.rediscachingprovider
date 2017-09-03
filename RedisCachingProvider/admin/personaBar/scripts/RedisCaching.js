'use strict';
define(['jquery',
    'main/config',
    'main/loader'
],
    function ($, cf, loader) {
        var initCallback;
        var utility;
        var settings;
        var config = cf.init();

        function loadScript(basePath) {
            var normalizedCulture = config.culture.split("-")[0];
            var language = getBundleLanguage(normalizedCulture);
            var url = basePath + "/bundle-" + language + ".js";
            $.ajax({
                dataType: "script",
                cache: true,
                url: url
            });
        }

        function getBundleLanguage(culture) {
            var fallbackLanguage = "en";
            var availableLanguages = ["en"];
            return availableLanguages.indexOf(culture) > 0 ? culture : fallbackLanguage;
        }


        return {
            init: function (wrapper, util, params, callback) {
                initCallback = callback;
                utility = util;
                settings = params.settings;

                if (!settings) {
                    throw new Error("Redis Caching Provider settings are not defined in persona bar customSettings");
                }

                var publicPath = settings.uiUrl + "/scripts/bundles/";
                window.dnn.initRedisCaching = function initializeRedisCaching() {
                    return {
                        publicPath: publicPath,
                        apiServiceUrl: settings.apiUrl,
                        libraryVersion: settings.libraryVersion,
                        loader: loader,
                        utility: util,
                        moduleName: 'RedisCaching',
                        notifier: {
                            confirm: util.confirm,
                            notify: util.notify,
                            notifyError: util.notifyError
                        }
                    };
                }
                loadScript(publicPath);

                if (typeof callback === "function") {
                    callback();
                }
            },

            load: function (params, callback) {
                var redisCaching = window.dnn.redisCaching;
                if (redisCaching && redisCaching.load) {
                    redisCaching.load();
                }

                if (typeof callback === "function") {
                    callback();
                }
            }
        };
    });
