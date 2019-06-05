import React from "react";
import { render } from "react-dom";
import { Provider } from "react-redux";
import configureStore from "./store/configureStore";
import Root from "./containers/Root";
import application from "./globals/application";

let store = configureStore({redisCachingProvider: "", redisOutputcachingProvider: ""});

application.dispatch = store.dispatch;
application.init();

const appContainer = document.getElementById("redisCaching-container");
render(
    <Provider store={store}>
        <Root />
    </Provider>,
    appContainer
);    

