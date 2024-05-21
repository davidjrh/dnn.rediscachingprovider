import React, {Component} from "react";
import PropTypes from "prop-types";
import { connect } from "react-redux";
import {PersonaBarPage, PersonaBarPageHeader, PersonaBarPageBody, Button, SingleLineInputWithError, Label, Switch} from "@dnnsoftware/dnn-react-common";
import resx from "../resources";
import util from "../utils";
import {
    settings as SettingsActions
} from "../actions";

import "./style.less";

class App extends Component {

    constructor() {
        super();

        this.state = {
            settingsLoaded: false,
            connectionString: "",
            cachingProviderEnabled: false,
            outputCachingProviderEnabled: false,
            useCompression: false,
            silentMode: true,
            keyPrefix: "",
            clientModified: false,
            error: {
                connectionString: false,
                keyPrefix: false
            },
            triedToSubmit: false
        };
    }

    UNSAFE_componentWillMount() {
        const {props} = this;

        if (props.settingsLoaded) {
            this.setState({
                connectionString: props.connectionString,
                cachingProviderEnabled: props.cachingProviderEnabled,
                outputCachingProviderEnabled: props.outputCachingProviderEnabled,
                useCompression: props.useCompression,
                silentMode: props.silentMode,       
                keyPrefix: props.keyPrefix,         
                clientModified: props.clientModified
            });
            return;
        }

        props.dispatch(SettingsActions.getSettings());        
    }

    UNSAFE_componentWillReceiveProps(props) {
        this.setState({
            connectionString: props.connectionString,
            cachingProviderEnabled: props.cachingProviderEnabled,
            outputCachingProviderEnabled: props.outputCachingProviderEnabled,
            useCompression: props.useCompression,
            silentMode: props.silentMode,
            keyPrefix: props.keyPrefix,
            clientModified: props.clientModified,
            triedToSubmit: false
        });
    }    

    onSettingChange(key, event) {
        let {state, props} = this;

        if (key === "ConnectionString") {
            state.connectionString = event.target.value;
        }

        if (key === "CachingProviderEnabled") {
            state.cachingProviderEnabled = !state.cachingProviderEnabled;
        }

        if (key === "OutputCachingProviderEnabled") {
            state.outputCachingProviderEnabled = !state.outputCachingProviderEnabled;
        }

        if (key === "UseCompression") {
            state.useCompression = !state.useCompression;
        }        

        if (key === "SilentMode") {
            state.silentMode = !state.silentMode;
        }     
        
        if (key === "KeyPrefix") {
            state.keyPrefix = event.target.value;
        }
        
        
        state.error["connectionString"] = (state.cachingProviderEnabled || state.outputCachingProviderEnabled) && state.connectionString.trim().length === 0;

        let pattern = /^[0-9a-zA-Z\-_]{1,20}$/i;            
        state.error["keyPrefix"] = state.keyPrefix.length > 0 && !pattern.test(state.keyPrefix);
        
        this.setState({
            connectionString: state.connectionString,
            cachingProviderEnabled: state.cachingProviderEnabled,
            outputCachingProviderEnabled: state.outputCachingProviderEnabled,
            useCompression: state.useCompression,
            silentMode: state.silentMode,
            keyPrefix: state.keyPrefix,
            error: state.error,
            clientModified: true,
            triedToSubmit: false
        });

        props.dispatch(SettingsActions.settingsClientModified({
            connectionString: state.connectionString,
            cachingProviderEnabled: state.cachingProviderEnabled,
            outputCachingProviderEnabled: state.outputCachingProviderEnabled,
            useCompression: state.useCompression,
            silentMode: state.silentMode,
            keyPrefix: state.keyPrefix
        }));
    }

    onCancel() {
        const {props} = this;
        util.utilities.confirm(resx.get("SettingsRestoreWarning"), resx.get("Yes"), resx.get("No"), () => {
            props.dispatch(SettingsActions.getSettings((data) => {
                this.setState({
                    connectionString: data.connectionString,
                    cachingProviderEnabled: data.cachingProviderEnabled,
                    outputCachingProviderEnabled: data.outputCachingProviderEnabled,
                    useCompression: data.useCompression,
                    silentMode: data.silentMode,    
                    keyPrefix: data.keyPrefix,
                    clientModified: false,
                    error: {
                        connectionString: false
                    }               
                });
            }));
        });        
    }

    onUpdate() {
        event.preventDefault();
        const {props, state} = this;

        if (state.error.connectionString)
            return;

        this.setState({
            triedToSubmit: true
        });

        props.dispatch(SettingsActions.updateSettings({
            connectionString: state.connectionString,
            cachingProviderEnabled: state.cachingProviderEnabled,
            outputCachingProviderEnabled: state.outputCachingProviderEnabled,
            useCompression: state.useCompression,
            silentMode: state.silentMode,
            keyPrefix: state.keyPrefix
        }, () => {
            util.utilities.notify(resx.get("SettingsUpdateSuccess"));
            this.setState({
                clientModified: false
            });            
        }, () => {
            util.utilities.notifyError(resx.get("SettingsError"));
        }));
    }
    render() {
        const {state} = this;
        return (
            <div id="RedisCachingAppContainer">
                <PersonaBarPage isOpen="true">
                    <PersonaBarPageHeader title="Redis Caching">
                    </PersonaBarPageHeader>
                    <PersonaBarPageBody>
                        <h1>General settings</h1>
                        <p>This caching provider allows you to use a Redis cache server/cluster in a DNN installation, using a hybrid in-memory approach to 
                            increase cache performance (items are cached in the local memory and on Redis cache), and the publisher/subscriber feature to keep in sync 
                            all the in-memory caches from the webfarm.</p>

                        <div className="row-30">
                            <Label label={resx.get("plCachingProviderEnabled") } style={{ fontWeight: "bold" }}/>
                            <div className="left">
                                <Switch labelHidden={true}
                                    value={state.cachingProviderEnabled}
                                    onChange={this.onSettingChange.bind(this, "CachingProviderEnabled") }/>
                            </div>
                        </div>

                        <div className="row-30">
                            <Label label={resx.get("plOutputCachingProviderEnabled") } style={{ fontWeight: "bold" }}/>
                            <div className="left">
                                <Switch labelHidden={true}
                                    value={state.outputCachingProviderEnabled}
                                    onChange={this.onSettingChange.bind(this, "OutputCachingProviderEnabled") } />
                            </div>
                        </div>                                                                    

                        <div className="row-100">
                            <SingleLineInputWithError
                                withLabel={true}
                                label={resx.get("plConnectionString") }
                                enabled={state.cachingProviderEnabled || state.outputCachingProviderEnabled}
                                error={this.state.error.connectionString}
                                errorMessage={resx.get("plConnectionString.Help")}                                
                                value={state.connectionString || ""}
                                onChange={this.onSettingChange.bind(this, "ConnectionString") } />
                        </div>

                        <h3>Advanced settings</h3>
                        <p>To save memory space, you can enable the compression option with a penalty in performance. The silent mode keeps the 
                            site running during the caching of non serializable objects. Keep an eye on the log4net logs to check if any 3rd party 
                            module has problems with out of process caching. 
                        </p>
                        <div className="row-30">
                            <Label label={resx.get("plUseCompression") } style={{ fontWeight: "bold" }}/>
                            <div className="left">
                                <Switch labelHidden={true}
                                    value={state.useCompression}
                                    onChange={this.onSettingChange.bind(this, "UseCompression") } />
                            </div>
                        </div>  

                        <div className="row-30">
                            <Label label={resx.get("plSilentMode") } style={{ fontWeight: "bold" }}/>
                            <div className="left">
                                <Switch labelHidden={true}
                                    value={state.silentMode}
                                    onChange={this.onSettingChange.bind(this, "SilentMode") } />
                            </div>
                        </div>  
                        <p>Finally, you can specify a key prefix to set on all the cached items, 
                            so you can share the Redis server with other DNN instances. If you do not specify any, a default prefix will be applied.</p>
                        <div className="row-100">
                            <SingleLineInputWithError
                                withLabel={true}
                                label={resx.get("plKeyPrefix") }
                                error={this.state.error.keyPrefix}
                                errorMessage={resx.get("plKeyPrefix.Help")}                                
                                value={state.keyPrefix || ""}
                                onChange={this.onSettingChange.bind(this, "KeyPrefix") } />
                        </div>                        

                        <div className="buttons-box">
                            <Button
                                disabled={!state.clientModified}
                                type="secondary"
                                onClick={this.onCancel.bind(this) }>
                                {resx.get("Cancel") }
                            </Button>
                            <Button
                                disabled={!state.clientModified || state.error.instrumentationKey || state.error.keyPrefix}
                                type="primary"
                                onClick={this.onUpdate.bind(this) }>
                                {resx.get("Update")}
                            </Button>
                        </div>
                    </PersonaBarPageBody>
                </PersonaBarPage>
            </div>
        );
    }
}

App.PropTypes = {
    dispatch: PropTypes.func.isRequired,
    connectionString: PropTypes.string,
    cachingProviderEnabled: PropTypes.bool,
    outputCachingProviderEnabled: PropTypes.bool,
    useCompression: PropTypes.bool,
    silentMode: PropTypes.bool,   
    clientModified: PropTypes.bool,
    keyPrefix: PropTypes.string
};

function mapStateToProps(state) {
    return {
        connectionString: state.settings.connectionString,
        cachingProviderEnabled: state.settings.cachingProviderEnabled,
        outputCachingProviderEnabled: state.settings.outputCachingProviderEnabled,
        useCompression: state.settings.useCompression,
        silentMode: state.settings.silentMode, 
        keyPrefix: state.settings.keyPrefix,       
        clientModified: state.settings.clientModified
    };
}

export default connect(mapStateToProps)(App);