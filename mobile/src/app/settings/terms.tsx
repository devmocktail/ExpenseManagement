import MaterialCommunityIcons from '@expo/vector-icons/MaterialCommunityIcons';
import { useRouter } from 'expo-router';
import { ScrollView, StyleSheet, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { AppText } from '@/components/ui/AppText';
import { ScreenHeader } from '@/components/ui/ScreenHeader';
import { useAppTheme } from '@/theme/ThemeProvider';

/**
 * Terms of service — placeholder copy.
 *
 * Written to be readable and to describe the product honestly. Anything that
 * would be a specific legal claim — governing law, liability caps, the
 * contracting entity — is left bracketed rather than invented, because a
 * plausible-looking clause is more dangerous than an obvious blank.
 */
export default function TermsOfServiceScreen() {
  const theme = useAppTheme();
  const router = useRouter();
  const insets = useSafeAreaInsets();

  return (
    <View style={[styles.flex, { backgroundColor: theme.c.background }]}>
      <ScreenHeader title="Terms of service" onBack={() => router.back()} />

      <ScrollView
        contentContainerStyle={{
          padding: theme.spacing.base,
          paddingBottom: insets.bottom + theme.spacing.xxl,
          gap: theme.spacing.lg,
        }}
        showsVerticalScrollIndicator={false}
      >
        <TemplateBanner />

        <View>
          <AppText variant="heading2" accessibilityRole="header">
            Terms of service
          </AppText>
          <AppText variant="caption" color="textTertiary" style={{ marginTop: theme.spacing.xs }}>
            Last updated: [date] · Between you and [company name]
          </AppText>
        </View>

        <Section title="Using the app">
          <Paragraph>
            Creating an account means you accept these terms. If you do not accept them, do not use
            the app. You need to be old enough to enter a binding agreement where you live. [Confirm
            the minimum age for each market.]
          </Paragraph>
        </Section>

        <Section title="Your account">
          <Paragraph>
            You are responsible for keeping your password and your device secure, and for everything
            done through your account. Tell us promptly at [contact email] if you think someone else
            has access. Changing your password signs out every other device.
          </Paragraph>
        </Section>

        <Section title="What this app is not">
          <Paragraph>
            It is a personal record-keeping tool. It does not provide financial, investment, tax or
            legal advice, it is not a bank or a payment service, and it does not move money. Nothing
            it shows you — including budgets, trends and projections — is a recommendation.
          </Paragraph>
          <Paragraph>
            Figures are only as accurate as what you enter. Check anything that matters against your
            bank or your accountant before relying on it.
          </Paragraph>
        </Section>

        <Section title="Your content">
          <Paragraph>
            Your transactions, notes and receipt images remain yours. You grant us only the
            permission needed to store, process and display them back to you, and to keep backups
            for the retention period described in the privacy policy. That permission ends when you
            delete the content or your account.
          </Paragraph>
        </Section>

        <Section title="Acceptable use">
          <Paragraph>
            Do not use the app for anything unlawful; do not attempt to access other users data,
            probe or overload the service, or work around its security; and do not resell or
            redistribute it. [State the consequences of a breach, including suspension and
            termination.]
          </Paragraph>
        </Section>

        <Section title="Availability and changes">
          <Paragraph>
            We aim to keep the service running but do not promise uninterrupted availability:
            maintenance, outages and dependencies outside our control all happen. Features may be
            added, changed or withdrawn. If a change materially reduces what you get, we will give
            notice in the app before it takes effect. [State any notice period committed to.]
          </Paragraph>
        </Section>

        <Section title="Fees">
          <Paragraph>
            [Describe what is free, what is paid, how billing and renewals work, and the refund and
            cancellation policy — including the store rules that apply to in-app purchases.]
          </Paragraph>
        </Section>

        <Section title="Ending the agreement">
          <Paragraph>
            You can stop at any time by deleting your account in Settings, which erases your data as
            described in the privacy policy. We may suspend or end an account that breaches these
            terms. [Set out notice and appeal, and which clauses survive termination.]
          </Paragraph>
        </Section>

        <Section title="Warranties, liability and disputes">
          <Paragraph>
            [These clauses must be drafted for the jurisdictions this app is published in — consumer
            protection law limits what can be disclaimed, and a copied clause is often
            unenforceable. Cover the warranty position, the liability cap, governing law, venue and
            any dispute-resolution process here.]
          </Paragraph>
        </Section>

        <Section title="Contact">
          <Paragraph>
            Questions about these terms go to [contact email], operated by [company name and
            registered address].
          </Paragraph>
        </Section>
      </ScrollView>
    </View>
  );
}

/** Shown at the top of both policy screens so a draft can never ship unnoticed. */
function TemplateBanner() {
  const theme = useAppTheme();

  return (
    <View
      accessibilityRole="alert"
      style={[
        styles.banner,
        {
          gap: theme.spacing.sm,
          backgroundColor: theme.c.warningMuted,
          borderRadius: theme.radius.medium,
          padding: theme.spacing.md,
        },
      ]}
    >
      <MaterialCommunityIcons name="alert-outline" size={18} color={theme.c.warning} />
      <View style={styles.flex}>
        <AppText variant="bodySmallStrong" color="warning">
          Template — not legal advice
        </AppText>
        <AppText variant="caption" color="textSecondary" style={{ marginTop: 2 }}>
          Placeholder copy for layout and review. It must be replaced with text approved by a
          qualified adviser, and every [bracketed] item filled in, before release.
        </AppText>
      </View>
    </View>
  );
}

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  const theme = useAppTheme();

  return (
    <View style={{ gap: theme.spacing.sm }}>
      <AppText variant="heading3" accessibilityRole="header">
        {title}
      </AppText>
      {children}
    </View>
  );
}

function Paragraph({ children }: { children: React.ReactNode }) {
  return (
    <AppText variant="bodySmall" color="textSecondary">
      {children}
    </AppText>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  banner: {
    flexDirection: 'row',
    alignItems: 'flex-start',
  },
});
