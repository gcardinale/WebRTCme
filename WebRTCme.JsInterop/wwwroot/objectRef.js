﻿objectRef = (function () {

    let public = {};

    /*
     *
     * private
     *
     */

    // Use same value on .NET side, "JsObjectRef.cs".
    const jsObjectRefKey = '__jsObjectRefId';

    // Use same value on .NET side, "JsEventHandler.cs".
    const jsEventRefKey = '__jsEventRefHandler';

    let objectRefs = {};
    let objectRefId = 0;

    let eventListenerId = 0;
    let eventListeners = {};

    DotNet.attachReviver(function (key, value) {
        if (value && typeof value === 'object' && value.hasOwnProperty(jsObjectRefKey) &&
            typeof value[jsObjectRefKey] === 'number') {
            let id = value[jsObjectRefKey];
            if (!(id in objectRefs)) {
                throw new Error('JS object reference with id:' + id + ' does not exist');
            }
            return objectRefs[id];
        } else {
            return value;
        }
    })

    DotNet.attachReviver(function (key, value) {
        if (value && typeof value === 'object' && value.hasOwnProperty(jsEventRefKey)) {
            var netObjectRef = value.callbackRef;
            return function () {
                var args = [];
                if (!value.getJsObjectRef) {
                    for (let index = 0; index < arguments.length; index++) {
                        const element = arguments[index];
                        args.push(getObjectContent(element, [], value.contentSpec));
                    }
                } else {
                    for (let index = 0; index < arguments.length; index++) {
                        const element = arguments[index];
                        args.push(addObjectRef(element));
                    }
                }
                return netObjectRef.invokeMethodAsync('Invoke', ...args);
            };
        } else {
            return value;
        }
    });

    addObjectRef = function (object) {
        let id = objectRefId++;
        objectRefs[id] = object;
        let objectRef = {};
        objectRef[jsObjectRefKey] = id;
        return objectRef;
    }

    getObject = function (parentObject, property) {
        if (parentObject === null) {
            parentObject = window;
        }
        let list = property.replace('[', '.').replace(']', '').split('.');
        if (list[0] === "") {
            list.shift();
        }
        let object = parentObject;
        for (i = 0; i < list.length; i++) {
            if (list[i] in object) {
                object = object[list[i]];
            } else {
                throw new Error("Object referenced by " + property + " does not exist");
            }
        }
        return object;
    }

    //getInterfaceObject = function (parentObject, interface) {
    //    if (parentObject === null) {
    //        parentObject = window;
    //    }
    //    let list = interface.replace('[', '.').replace(']', '').split('.');
    //    if (list[0] === "") {
    //        list.shift();
    //    }
    //    let object = parentObject;
    //    for (i = 0; i < list.length; i++) {
    //        if (list[i] in object) {
    //            object = object[list[i]];
    //        } else {
    //            throw new Error("Object referenced by " + interface + " does not exist");
    //        }
    //    }
    //    return object;
    //}

    //getMethodObject = function (parentObject, method) {
    //    if (method.includes(".")) {
    //        let property = method.substring(0, method.lastIndexOf('.'));
    //        parentObject = getPropertyObject(parentObject, property);
    //        method = method.substring(method.lastIndexOf('.') + 1);
    //    }
    //    let object = getPropertyObject(parentObject, method);
    //    return object;
    //}

    getObjectContent = function (object, accumulatedContent, contentSpec) {
        if (contentSpec === false) {
            return undefined;
        }
        if (!accumulatedContent) {
            accumulatedContent = [];
        }
        if (typeof object == "undefined" || object === null) {
            return null;
        }
        if (typeof object === "number" || typeof object === "string" || typeof object == "boolean") {
            return object;
        }
        let content = (Array.isArray(object)) ? [] : {};
        if (!contentSpec) {
            contentSpec = "*";
        }
        for (let i in object) {
            let currentMember = object[i];
            if (typeof currentMember === 'function' || currentMember === null) {
                continue;
            }
            let currentMemberSpec;
            if (contentSpec != "*") {
                currentMemberSpec = Array.isArray(object) ? contentSpec : contentSpec[i];
                if (!currentMemberSpec) {
                    continue;
                }
            } else {
                currentMemberSpec = "*"
            }
            if (typeof currentMember === 'object') {
                if (accumulatedContent.indexOf(currentMember) >= 0) {
                    continue;
                }
                accumulatedContent.push(currentMember);
                if (Array.isArray(currentMember) || currentMember.length) {
                    content[i] = [];
                    for (let j = 0; j < currentMember.length; j++) {
                        const arrayItem = currentMember[j];
                        if (typeof arrayItem === 'object') {
                            content[i].push(getObjectContent(arrayItem, accumulatedContent, currentMemberSpec));
                        } else {
                            content[i].push(arrayItem);
                        }
                    }
                } else {
                    if (currentMember.length === 0) {
                        content[i] = [];
                    } else {
                        content[i] = getObjectContent(currentMember, accumulatedContent, currentMemberSpec);
                    }
                }
            } else {
                if (currentMember === Infinity) {
                    currentMember = "Infinity";
                }
                if (currentMember !== null) {
                    content[i] = currentMember;
                }
            }
        }
        return content;
    };

    /* 
     * 
     * public API
     * 
     */

    public.create = function (parentObject, interface, ...args) {
        let interfaceObject = getObject(parentObject, interface);
        let createdObject = new interfaceObject(args);
        let objectRef = addObjectRef(createdObject);
        return objectRef;
    }

    public.delete = function (id) {
        delete objectRefs[id];
    }

    public.get = function (parentObject, property) {
        let propertyObject = getObject(parentObject, property);
        let objectRef = addObjectRef(propertyObject);
        return objectRef;
    }

    public.set = function (parentObject, property, value) {
        if (parentObject === null) {
            parentObject = window;
        }
        parentObject[property] = value;
    }

    public.content = function (parentObject, property, contentSpec) {
        let propertyObject = {};
        if (property === null) {
            propertyObject = parentObject;
        } else {
            propertyObject = getObject(parentObject, property);
        }
        let content = getObjectContent(propertyObject, [], contentSpec);
        return content;
    }

    public.call = function (parentObject, method, ...args) {
        let methodObject = getObject(parentObject, method);
        let ret = methodObject.apply(parentObject, args);
        if (ret != undefined) {
            let objectRef = addObjectRef(ret);
            return objectRef;
        }
    }

    public.callAsync = async function (parentObject, method, ...args) {
        let methodObject = getObject(parentObject, method);
        let ret = await methodObject.apply(parentObject, args);
        if (ret != undefined) {
            let objectRef = addObjectRef(ret);
            return objectRef;
        }
    }

    public.addEventListener = function (parentObject, property, event, callback) {
        var target = getObject(parentObject, property);
        target.addEventListener(event, callback);
        var eventId = eventListenerId++;
        eventListeners[eventId] = callback;
        return eventId;
    };
    public.removeEventListener = function (parentObject, property, event, id) {
        var target = getObject(parentObject, property);
        target.removeEventListener(event, eventListeners[id]);
        delete eventListeners[id];
    };

    return public;

})();