import { settings as ActionTypes } from "../constants/actionTypes";

export default function settings(state = {
    connectionString: "",
    cachingProviderEnabled: false,
    outputCachingProviderEnabled: false,
    useCompression: false,
    silentMode: true,
    clientModified: false,
    keyPrefix: ""
}, action) {
    switch (action.type) {
        case ActionTypes.RETRIEVED_SETTINGS:
            return { ...state,
                connectionString: action.data.connectionString,
                cachingProviderEnabled: action.data.cachingProviderEnabled,
                outputCachingProviderEnabled: action.data.outputCachingProviderEnabled,
                useCompression: action.data.useCompression,
                silentMode: action.data.silentMode,
                keyPrefix: action.data.keyPrefix,
                clientModified: action.data.clientModified
            };
        case ActionTypes.SETTINGS_CLIENT_MODIFIED:
            return { ...state,
                connectionString: action.data.connectionString,                
                cachingProviderEnabled: action.data.cachingProviderEnabled,
                outputCachingProviderEnabled: action.data.outputCachingProviderEnabled,
                useCompression: action.data.useCompression,
                silentMode: action.data.silentMode,   
                keyPrefix: action.data.keyPrefix,             
                clientModified: action.data.clientModified
            };
        case ActionTypes.UPDATED_SETTINGS:
            return { ...state,
                clientModified: action.data.clientModified
            };            
        default:
            return { ...state
            };
    }
}
