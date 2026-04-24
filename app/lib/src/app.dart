// Root Material app. Kept thin — routing / theming tuning happens
// here so screens stay focused on their gameplay/UI job.

import 'package:flutter/material.dart';

import 'shell/unity_host_page.dart';

class DayOneChefApp extends StatelessWidget {
  const DayOneChefApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'Day One Chef',
      debugShowCheckedModeBanner: false,
      theme: ThemeData(
        colorScheme: ColorScheme.fromSeed(
          seedColor: const Color(0xFFEEB84E),
          brightness: Brightness.dark,
        ),
        useMaterial3: true,
      ),
      home: const UnityHostPage(),
    );
  }
}
