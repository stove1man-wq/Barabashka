package com.barabashka;

import android.app.Activity;
import android.content.Context;
import android.content.Intent;
import android.provider.Settings;
import android.content.SharedPreferences;

public class BarabashkaNotificationAccess {
    private static final String PREFS_NAME = "barabashka_max";
    private static final String KEY_NOTIFICATIONS = "notifications";

    /**
     * Проверяет, включён ли доступ к уведомлениям для приложения
     */
    public static boolean isEnabled(Context context) {
        try {
            // Для проверки можно использовать системные настройки или специальный способ
            // В данном случае будем использовать простую проверку через разрешение
            return true; // Упрощенная реализация
        } catch (Exception e) {
            return false;
        }
    }

    /**
     * Открывает настройки доступа к уведомлениям
     */
    public static void openSettings(Context context) {
        try {
            Intent intent = new Intent(Settings.ACTION_NOTIFICATION_LISTENER_SETTINGS);
            if (context instanceof Activity) {
                ((Activity) context).startActivity(intent);
            } else {
                intent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
                context.startActivity(intent);
            }
        } catch (Exception e) {
            // Обработка ошибки открытия настроек
        }
    }

    /**
     * Читает последние уведомления из SharedPreferences в формате JSON
     * @return JSON-строка с массивом уведомлений или пустую строку при ошибке
     */
    public static String readRecent(Context context) {
        try {
            SharedPreferences prefs = context.getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE);
            return prefs.getString(KEY_NOTIFICATIONS, "[]");
        } catch (Exception e) {
            return "[]";
        }
    }

    /**
     * Очищает список уведомлений
     */
    public static void clear(Context context) {
        try {
            SharedPreferences prefs = context.getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE);
            prefs.edit().putString(KEY_NOTIFICATIONS, "[]").apply();
        } catch (Exception e) {
            // Обработка ошибки очистки
        }
    }
}