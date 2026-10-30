package com.barabashka;

import android.app.Notification;
import android.content.Context;
import android.content.SharedPreferences;
import android.os.Bundle;
import android.service.notification.NotificationListenerService;
import android.service.notification.StatusBarNotification;
import android.util.Log;

import org.json.JSONArray;
import org.json.JSONException;
import org.json.JSONObject;

import java.text.SimpleDateFormat;
import java.util.Date;
import java.util.Locale;

public class BarabashkaNotificationListener extends NotificationListenerService {
    private static final String TAG = "BarabashkaNotificationListener";
    private static final String PREFS_NAME = "barabashka_max";
    private static final String KEY_NOTIFICATIONS = "notifications";
    private static final int MAX_NOTIFICATIONS = 50;

    @Override
    public void onNotificationPosted(StatusBarNotification sbn) {
        super.onNotificationPosted(sbn);
        
        // Проверяем, что это уведомление от приложения ru.oneme.app
        if ("ru.oneme.app".equals(sbn.getPackageName())) {
            Notification notification = sbn.getNotification();
            Bundle extras = notification.extras;
            
            // Извлекаем данные из уведомления
            String title = extras.getString(Notification.EXTRA_TITLE, "");
            String text = extras.getString(Notification.EXTRA_TEXT, "");
            long time = sbn.getPostTime();
            
            // Сохраняем в SharedPreferences как JSON-массив
            saveNotificationToPrefs(title, text, time);
        }
    }

    private void saveNotificationToPrefs(String title, String text, long time) {
        try {
            SharedPreferences prefs = getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE);
            String json = prefs.getString(KEY_NOTIFICATIONS, "[]");
            JSONArray notificationsArray = new JSONArray(json);
            
            // Создаем новый элемент уведомления
            JSONObject notificationJson = new JSONObject();
            notificationJson.put("title", title);
            notificationJson.put("text", text);
            notificationJson.put("time", time);
            
            // Добавляем новый элемент в начало массива
            notificationsArray.put(0, notificationJson);
            
            // Ограничиваем до 50 последних уведомлений
            if (notificationsArray.length() > MAX_NOTIFICATIONS) {
                JSONArray trimmedArray = new JSONArray();
                for (int i = 0; i < MAX_NOTIFICATIONS; i++) {
                    trimmedArray.put(notificationsArray.get(i));
                }
                notificationsArray = trimmedArray;
            }
            
            // Сохраняем обновлённый массив
            prefs.edit().putString(KEY_NOTIFICATIONS, notificationsArray.toString()).apply();
            
        } catch (JSONException e) {
            Log.e(TAG, "Ошибка при сохранении уведомления", e);
        }
    }
}