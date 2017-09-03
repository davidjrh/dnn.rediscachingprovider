import { settings as ActionTypes } from "../constants/actionTypes";
import ApplicationService from "../services/applicationService";

const settingsActions = {
    getSettings(callback) {
        return (dispatch) => {
            ApplicationService.getSettings(data => {
                dispatch({
                    type: ActionTypes.RETRIEVED_SETTINGS,
                    data: {
                        connectionString: data.connectionString,
                        cachingProviderEnabled: data.cachingProviderEnabled,
                        outputCachingProviderEnabled: data.outputCachingProviderEnabled,
                        useCompression: data.useCompression,
                        silentMode: data.silentMode,
                        keyPrefix: data.keyPrefix,                        
                        clientModified: false
                    }
                });
                if (callback) {
                    callback(data);
                }
            });
        };
    },
    updateSettings(payload, callback, failureCallback) {
        return (dispatch) => {
            ApplicationService.updateSettings(payload, data => {
                dispatch({
                    type: ActionTypes.UPDATED_SETTINGS,
                    data: {
                        clientModified: false
                    }
                });
                if (callback) {
                    callback(data);
                }
            }, data => {
                if (failureCallback) {
                    failureCallback(data);
                }
            });
        };
    },
    settingsClientModified(settings) {
        return (dispatch) => {
            dispatch({
                type: ActionTypes.SETTINGS_CLIENT_MODIFIED,
                data: {
                    connectionString: settings.connectionString,
                    cachingProviderEnabled: settings.cachingProviderEnabled,
                    outputCachingProviderEnabled: settings.outputCachingProviderEnabled,
                    useCompression: settings.useCompression,
                    silentMode: settings.silentMode,
                    keyPrefix: settings.keyPrefix,                    
                    clientModified: true
                }
            });
        };
    }
};

export default settingsActions;